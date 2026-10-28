using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

/// <summary>
/// Draws every entity in the world that has a <see cref="Transform"/> and a
/// <see cref="MeshRenderer"/>, using the first entity that carries a <see cref="Camera"/>.
/// </summary>
internal unsafe class SceneRenderer : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly MeshFactory _meshFactory;
    private readonly SceneSession _session;
    private readonly List<DrawSubmission> _submissions = [];

    /// <summary>The world being rendered: the runtime copy during play, else the authored world.</summary>
    private World _world => _session.Active;

    //NOTE: The two view images are full swapchain sized and get stretched into their ImGui
    //panels, so each camera's projection aspect has to match the panel it is displayed in.
    private Vector2 _editorViewSize;
    private Vector2 _gameViewSize;

    private DescriptorSet _descriptorSet;
    private PipelineData _geometryPipeline;

    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    /// <summary>Camera driving the editor viewport.</summary>
    public Camera? EditorCamera { get; private set; }

    /// <summary>Camera driving the game view; falls back to the editor camera when there is none.</summary>
    public Camera? GameCamera { get; private set; }

    public SceneRenderer(VulkanMaster master, MeshFactory meshFactory, SceneSession session)
    {
        _master = master;
        _meshFactory = meshFactory;
        _session = session;

        RefreshCameras();
        Debug.Log($"SCENE RENDERER: {_world.EntityCount} entities, editor camera: {EditorCamera is not null}, " +
                  $"game camera: {GameCamera is not null}", VALIDATION_LAYERS.INFO);
    }

    public void SetViewSizes(Vector2 editorView, Vector2 gameView)
    {
        if (editorView.X > 1f && editorView.Y > 1f)
            _editorViewSize = editorView;

        if (gameView.X > 1f && gameView.Y > 1f)
            _gameViewSize = gameView;
    }

    public void NewFrame()
    {
        RefreshCameras();
        BuildDrawSubmissions();
    }

    private void RefreshCameras()
    {
        // The gizmo moves the entity transform, so every camera follows its own transform.
        SceneCameras.SyncFromTransforms(_world);

        EditorCamera = FindCamera(CameraKind.Editor, anyKind: true);
        GameCamera = FindCamera(CameraKind.Game) ?? EditorCamera;
    }

    private Camera? FindCamera(CameraKind kind, bool anyKind = false)
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
        {
            var camera = _world.GetComponent<Camera>(entity);

            if (anyKind || camera.Kind == kind)
                return camera;
        }

        return null;
    }

    private void BuildDrawSubmissions()
    {
        EnsureGeometryResources();

        if (!_geometryPipeline.IsValid)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        _submissions.Clear();
        var frameIndex = _master.FrameHandler?.CurrentFrame ?? 0;

        // View 0 is the editor viewport, view 1 the game view; both share the scene geometry.
        AddView(frameIndex, viewIndex: 0, RenderPassType.EditorView, EditorCamera, _editorViewSize);

        if (!ReferenceEquals(GameCamera, EditorCamera))
            AddView(frameIndex, viewIndex: 1, RenderPassType.GameView, GameCamera, _gameViewSize);

        CurrentSubmissions = _submissions.ToArray();
    }

    private void AddView(uint frameIndex, uint viewIndex, RenderPassType passType, Camera? camera, Vector2 viewSize)
    {
        if (camera is null)
            return;

        var descriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex, viewIndex);
        UpdateCameraUniformBuffer(camera, frameIndex, viewIndex, viewSize);

        foreach (var entity in _world.GetEntitiesWith<Transform, MeshRenderer>())
        {
            var meshRenderer = _world.GetComponent<MeshRenderer>(entity);
            if (!_meshFactory.TryGet(meshRenderer.Mesh, out var mesh))
                continue;

            var model = _world.GetComponent<Transform>(entity).GetWorldMatrix(_world);

            var objectConstants = new ObjectPushConstants
            {
                Model = model,
                BaseColor = meshRenderer.Material.BaseColor,
            };

            var payload = new byte[ObjectPushConstants.SizeInBytes];
            MemoryMarshal.Write(payload.AsSpan(), in objectConstants);

            _submissions.Add(new DrawSubmission
            {
                PassType = passType,
                PipelineData = _geometryPipeline,
                DescriptorSet = descriptorSet,
                VertexBuffer = mesh.VertexBuffer,
                VertexOffset = 0,
                IndexBuffer = mesh.IndexBuffer,
                IndexOffset = 0,
                IndexType = mesh.IndexType,
                VertexCount = mesh.VertexCount,
                IndexCount = mesh.IndexCount,
                InstanceCount = 1,
                FirstVertex = 0,
                FirstIndex = 0,
                VertexBase = 0,
                ScissorPolicy = SubmissionScissorPolicy.PassDefault,
                Scissor = default,
                ViewportPolicy = SubmissionViewportPolicy.PassDefault,
                Viewport = default,
                PushConstants = new PushConstantPayload
                {
                    StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                    Offset = 0,
                    Data = payload,
                },
                ModelMatrix = model,
            });
        }
    }

    private void UpdateCameraUniformBuffer(Camera camera, uint frameIndex, uint viewIndex, Vector2 viewSize)
    {
        if (viewSize is { X: > 1f, Y: > 1f })
        {
            camera.SetAspectRatio(viewSize.X, viewSize.Y);
        }

        var buffer = _master.DescriptorFactory.GetDefaultUniformBuffer(frameIndex, viewIndex);
        if (!buffer.IsValid || !buffer.HostVisible)
            return;

        // System.Numerics matrices are row-major/row-vector, which maps directly onto the
        // column-major mat4 the shader reads, so the matrix is uploaded as-is.
        var cameraData = new CameraUboData
        {
            ViewProjection = camera.GetViewProjectionMatrix()
        };

        void* mapped = null;
        var dataSize = (ulong)Marshal.SizeOf<CameraUboData>();
        var mapResult = _master.Vk.MapMemory(_master.VulkanDevice.Device, buffer.Memory, 0, dataSize, 0, &mapped);
        if (mapResult != Result.Success || mapped is null)
            throw new InvalidOperationException($"Failed to map camera uniform buffer for frame {frameIndex}. Result: {mapResult}");

        *(CameraUboData*)mapped = cameraData;
        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, buffer.Memory);
    }

    private void EnsureGeometryResources()
    {
        if (_geometryPipeline.IsValid)
            return;

        var renderPassKey = new RenderPassKey
        {
            ColorFormat = Format.R16G16B16A16Sfloat,
            DepthFormat = Format.D32Sfloat,
            HasDepth = true,
            HasAlpha = true,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
            InitialDepthLayout = ImageLayout.Undefined,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        var renderPass = _master.RenderPassFactory.CreateRenderPass(renderPassKey);

        var vertexFormat = new VertexFormat
        {
            Stride = (uint)Marshal.SizeOf<Vertex>(),
            Attributes =
            [
                VertexAttribute.Create<Vertex>(0, Format.R32G32B32Sfloat, nameof(Vertex.Position)),
                VertexAttribute.Create<Vertex>(1, Format.R32G32B32Sfloat, nameof(Vertex.Normal)),
            ]
        };

        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = "default_shader.vert.spv",
            FragmentShaderPath = "default_shader.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = vertexFormat,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
            PushConstantSize = (uint)ObjectPushConstants.SizeInBytes,
            PushConstantStages = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
        };

        _geometryPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
    }

    public void Dispose()
    {
        CurrentSubmissions = Array.Empty<DrawSubmission>();
        _submissions.Clear();
    }
}
