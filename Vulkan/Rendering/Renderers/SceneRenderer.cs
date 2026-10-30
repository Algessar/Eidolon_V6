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
    private PipelineData _skinnedPipeline;
    private PipelineData _outlinePipeline;
    private PipelineData _skinnedOutlinePipeline;

    /// <summary>Every skin's bone matrices for this frame, packed into one buffer upload.</summary>
    private readonly Matrix4x4[] _bonePalette = new Matrix4x4[DescriptorFactory.BoneMatrixCapacity];

    /// <summary>Where each skin's slice starts in <see cref="_bonePalette"/>; rebuilt every frame.</summary>
    private readonly Dictionary<SkinnedMesh, int> _skinOffsets = new();

    private int _paletteCount;

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

        UploadBonePalettes(frameIndex);

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
            var model = _world.GetComponent<Transform>(entity).GetWorldMatrix(_world);

            if (TryAddSkinnedView(entity, model, passType, frameIndex, viewIndex))
                continue;

            if (!_meshFactory.TryGet(meshRenderer.Mesh, out var mesh))
                continue;

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

        AddOutlines(passType, frameIndex, viewIndex);
    }

    /// <summary>
    /// Draws the selection rim for every highlighted entity. Outlines go after the geometry of the same
    /// view, because the hull is hidden by the object's own depth everywhere except around its silhouette.
    /// </summary>
    private void AddOutlines(RenderPassType passType, uint frameIndex, uint viewIndex)
    {
        if (!_outlinePipeline.IsValid)
            return;

        foreach (var entity in _world.GetEntitiesWith<Highlight>())
        {
            if (!_world.TryGetComponent<Transform>(entity, out _) ||
                !_world.TryGetComponent<MeshRenderer>(entity, out var meshRenderer))
            {
                continue;
            }

            var model = _world.GetComponent<Transform>(entity).GetWorldMatrix(_world);

            if (TryAddSkinnedOutline(entity, model, passType, frameIndex, viewIndex))
                continue;

            if (!_meshFactory.TryGet(meshRenderer.Mesh, out var mesh))
                continue;

            // Only the model matrix is pushed: expansion and colour live in the outline shaders.
            var payload = new byte[OutlinePushConstants.SizeInBytes];
            MemoryMarshal.Write(payload.AsSpan(), in model);

            _submissions.Add(new DrawSubmission
            {
                PassType = passType,
                PipelineData = _outlinePipeline,
                DescriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex, viewIndex),
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
                    StageFlags = ShaderStageFlags.VertexBit,
                    Offset = 0,
                    Data = payload,
                },
                ModelMatrix = model,
            });
        }
    }

    /// <summary>Outlines a skinned entity in its current pose, using its slice of the bone palette.</summary>
    private bool TryAddSkinnedOutline(Entity entity, Matrix4x4 model, RenderPassType passType,
        uint frameIndex, uint viewIndex)
    {
        if (!_skinnedOutlinePipeline.IsValid ||
            !_world.TryGetComponent<SkinnedMesh>(entity, out var skin) ||
            !_skinOffsets.TryGetValue(skin, out var boneOffset) ||
            !_meshFactory.TryGetSkinned(skin, out var mesh))
        {
            return false;
        }

        var constants = new SkinnedPushConstants { Model = model, BoneOffset = (uint)boneOffset };
        var payload = new byte[SkinnedPushConstants.SizeInBytes];
        MemoryMarshal.Write(payload.AsSpan(), in constants);

        _submissions.Add(new DrawSubmission
        {
            PassType = passType,
            PipelineData = _skinnedOutlinePipeline,
            DescriptorSet = _master.DescriptorFactory.GetSkinnedDescriptorSet(frameIndex, viewIndex),
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
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Data = payload,
            },
            ModelMatrix = model,
        });

        return true;
    }

    /// <summary>
    /// Draws a skinned entity from its bind pose and lets the vertex shader apply the bones.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the entity is not skinned or its skin did not fit in this frame's palette, so the
    /// caller falls back to drawing the mesh as it stands.
    /// </returns>
    private bool TryAddSkinnedView(Entity entity, Matrix4x4 model, RenderPassType passType,
        uint frameIndex, uint viewIndex)
    {
        if (!_world.TryGetComponent<SkinnedMesh>(entity, out var skin))
            return false;

        if (!_skinOffsets.TryGetValue(skin, out var boneOffset))
            return false;

        if (!_meshFactory.TryGetSkinned(skin, out var mesh))
            return false;

        var constants = new SkinnedPushConstants
        {
            Model = model,
            BaseColor = _world.GetComponent<MeshRenderer>(entity).Material.BaseColor,
            BoneOffset = (uint)boneOffset,
        };

        var payload = new byte[SkinnedPushConstants.SizeInBytes];
        MemoryMarshal.Write(payload.AsSpan(), in constants);

        _submissions.Add(new DrawSubmission
        {
            PassType = passType,
            PipelineData = _skinnedPipeline,
            DescriptorSet = _master.DescriptorFactory.GetSkinnedDescriptorSet(frameIndex, viewIndex),
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

        return true;
    }

    /// <summary>Packs every skinned mesh's bone matrices into this frame's palette.</summary>
    /// <remarks>
    /// One upload serves every skin: the shader indexes the palette at an offset carried by the object's
    /// push constants, so a scene full of skinned meshes costs one buffer write per frame rather than one
    /// per mesh. A skin that does not fit keeps drawing its bind pose.
    /// </remarks>
    private void UploadBonePalettes(uint frameIndex)
    {
        _skinOffsets.Clear();
        _paletteCount = 0;

        if (!_skinnedPipeline.IsValid)
            return;

        foreach (var entity in _world.GetEntitiesWith<SkinnedMesh>())
        {
            var skin = _world.GetComponent<SkinnedMesh>(entity);
            var remaining = _bonePalette.Length - _paletteCount;

            if (skin.Bones.Length == 0 || skin.Bones.Length > remaining)
                continue;

            var written = skin.ComputeBoneMatrices(_world, _bonePalette.AsSpan(_paletteCount));
            if (written == 0)
                continue;

            _skinOffsets[skin] = _paletteCount;
            _paletteCount += written;
        }

        if (_paletteCount == 0)
            return;

        var buffer = _master.DescriptorFactory.GetBoneBuffer(frameIndex);
        if (!buffer.IsValid || !buffer.HostVisible)
            return;

        var uploadSize = (ulong)_paletteCount * (ulong)Marshal.SizeOf<Matrix4x4>();

        void* mapped = null;
        var mapResult = _master.Vk.MapMemory(_master.VulkanDevice.Device, buffer.Memory, 0, uploadSize, 0, &mapped);
        if (mapResult != Result.Success || mapped is null)
            throw new InvalidOperationException($"Failed to map the bone palette for frame {frameIndex}. Result: {mapResult}");

        fixed (Matrix4x4* source = _bonePalette)
        {
            global::System.Buffer.MemoryCopy(source, mapped, uploadSize, uploadSize);
        }

        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, buffer.Memory);
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

        // Same pass and shader output as the geometry pipeline, but vertices arrive with their bone
        // blend and the bones are applied in the vertex shader.
        var skinnedFormat = new VertexFormat
        {
            Stride = (uint)Marshal.SizeOf<SkinnedVertex>(),
            Attributes =
            [
                VertexAttribute.Create<SkinnedVertex>(0, Format.R32G32B32Sfloat, nameof(SkinnedVertex.Position)),
                VertexAttribute.Create<SkinnedVertex>(1, Format.R32G32B32Sfloat, nameof(SkinnedVertex.Normal)),
                VertexAttribute.Create<SkinnedVertex>(2, Format.R32G32B32A32Sfloat, nameof(SkinnedVertex.BoneWeights)),
                VertexAttribute.Create<SkinnedVertex>(3, Format.R32G32B32A32Sfloat, nameof(SkinnedVertex.BoneIndices)),
            ]
        };

        var skinnedKey = new PipelineKey
        {
            VertexShaderPath = "skinned.vert.spv",
            FragmentShaderPath = "default_shader.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.SkinnedLayout,
            VertexFormat = skinnedFormat,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,

            // The colour sits at the same offset the unskinned block puts it at, so one fragment shader
            // reads both; the skin's palette offset rides along in the few bytes after it.
            PushConstantSize = (uint)SkinnedPushConstants.SizeInBytes,
            PushConstantStages = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
        };

        _skinnedPipeline = _master.PipelineFactory.GetOrCreate(skinnedKey);

        // Selection outlines: the same geometry, pushed out along its normals and drawn with the front
        // faces culled so only the rim shows. Depth write is off, so a hull never blocks what is behind it.
        _outlinePipeline = _master.PipelineFactory.GetOrCreate(new PipelineKey
        {
            VertexShaderPath = "outline.vert.spv",
            FragmentShaderPath = "outline.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = vertexFormat,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.Front,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = false,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
            PushConstantSize = (uint)OutlinePushConstants.SizeInBytes,
            PushConstantStages = ShaderStageFlags.VertexBit,
        });

        _skinnedOutlinePipeline = _master.PipelineFactory.GetOrCreate(new PipelineKey
        {
            VertexShaderPath = "outline_skinned.vert.spv",
            FragmentShaderPath = "outline.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.SkinnedLayout,
            VertexFormat = skinnedFormat,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.Front,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = false,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
            PushConstantSize = (uint)SkinnedPushConstants.SizeInBytes,
            PushConstantStages = ShaderStageFlags.VertexBit,
        });
    }

    public void Dispose()
    {
        CurrentSubmissions = Array.Empty<DrawSubmission>();
        _submissions.Clear();
    }
}
