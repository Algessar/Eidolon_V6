using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using Silk.NET.Vulkan;

namespace EidolonEngine;

/// <summary>
/// Draws every entity in the world that has a <see cref="Transform"/> and a
/// <see cref="MeshRenderer"/>, using the first entity that carries a <see cref="Camera"/>.
/// </summary>
internal unsafe class SceneRenderer : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly MeshFactory _meshFactory;
    private readonly World _world;
    private readonly List<DrawSubmission> _submissions = [];

    //NOTE: Size of the game view panel in the editor UI. The GameView image is the full
    //swapchain and gets stretched into that panel, so matching the projection aspect to the
    //panel keeps the scene free of distortion.
    private Vector2 _gameViewSize;

    private DescriptorSet _descriptorSet;
    private PipelineData _geometryPipeline;

    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    /// <summary>The scene camera, or null when the world has no entity carrying one.</summary>
    public Camera? ActiveCamera { get; private set; }

    public SceneRenderer(VulkanMaster master, MeshFactory meshFactory, World world)
    {
        _master = master;
        _meshFactory = meshFactory;
        _world = world;

        ActiveCamera = FindCamera();
        Debug.Log($"SCENE RENDERER: {_world.EntityCount} entities, camera: {ActiveCamera is not null}",
            VALIDATION_LAYERS.INFO);
    }

    public void SetGameViewSize(Vector2 size)
    {
        if (size.X > 1f && size.Y > 1f)
            _gameViewSize = size;
    }

    public void NewFrame()
    {
        ActiveCamera = FindCamera();
        BuildDrawSubmissions();
    }

    private Camera? FindCamera()
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
            return _world.GetComponent<Camera>(entity);

        return null;
    }

    private void BuildDrawSubmissions()
    {
        EnsureGeometryResources();

        var camera = ActiveCamera;
        if (!_geometryPipeline.IsValid || camera is null)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        var frameIndex = _master.FrameHandler?.CurrentFrame ?? 0;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex);
        UpdateCameraUniformBuffer(camera, frameIndex);

        _submissions.Clear();

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
                PassType = RenderPassType.GameView,
                PipelineData = _geometryPipeline,
                DescriptorSet = _descriptorSet,
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

        CurrentSubmissions = _submissions.ToArray();
    }

    private void UpdateCameraUniformBuffer(Camera camera, uint frameIndex)
    {
        if (_gameViewSize is { X: > 1f, Y: > 1f })
        {
            camera.SetAspectRatio(_gameViewSize.X, _gameViewSize.Y);
        }
        else if (_master.FrameHandler is { } frameHandler &&
                 (frameHandler.TryGetResourceExtent("GameView", out var sceneExtent) ||
                  frameHandler.TryGetResourceExtent("SceneColor", out sceneExtent)) &&
                 sceneExtent is { Width: > 0, Height: > 0 })
        {
            camera.SetAspectRatio(sceneExtent.Width, sceneExtent.Height);
        }

        var buffer = _master.DescriptorFactory.GetDefaultUniformBuffer(frameIndex);
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
