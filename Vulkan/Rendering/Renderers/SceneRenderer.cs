using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace EidolonEngine;

//NOTE: 
internal unsafe class SceneRenderer : IDisposable
{
    
    private readonly VulkanMaster _master;
    private readonly MeshFactory _meshFactory;
    private Camera _camera;
    public Camera Camera => _camera;

    //NOTE: Size of the game view panel in the editor UI. The GameView image is the full
    // swapchain and gets stretched into that panel, so matching the projection aspect to the
    // panel keeps the scene free of distortion.
    private Vector2 _gameViewSize;
    
    private DescriptorSet _descriptorSet;
    private PipelineData _geometryPipeline;
    private MeshHandle _cubeMesh;
     
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    public SceneRenderer(VulkanMaster master, MeshFactory meshFactory)
    {
        _master = master;
        _meshFactory = meshFactory;

        _camera = new Camera
        {
            Position = new Vector3(2f, 1.5f, 3.5f)
        };
        _camera.SetTarget(Vector3.Zero);
        Debug.Log($"SCENE RENDERER: CameraPos: {_camera.Position} :: Camera Target: {_camera.Target}", VALIDATION_LAYERS.INFO);

    }

    public void SetGameViewSize(Vector2 size)
    {
        if (size.X > 1f && size.Y > 1f)
            _gameViewSize = size;
    }
    
    public void NewFrame()
    {
        BuildDrawSubmissions();
    }

   public void BuildDrawSubmissions()
    {
        EnsureGeometryResources();

        if (!_geometryPipeline.IsValid)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        if (!_meshFactory.TryGet(_cubeMesh, out var mesh))
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }
        
        var frameIndex = _master.FrameHandler?.CurrentFrame ?? 0;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex);
        UpdateCameraUniformBuffer(frameIndex);
        
        CurrentSubmissions =
        [
            new DrawSubmission
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
                PushConstants = PushConstantPayload.Empty,
                ModelMatrix = Matrix4x4.Identity,
            }
        ];
    }

    private void UpdateCameraUniformBuffer(uint frameIndex)
    {
        if (_gameViewSize is { X: > 1f, Y: > 1f })
        {
            _camera.SetAspectRatio(_gameViewSize.X, _gameViewSize.Y);
        }
        else if (_master.FrameHandler is { } frameHandler &&
                 (frameHandler.TryGetResourceExtent("GameView", out var sceneExtent) ||
                  frameHandler.TryGetResourceExtent("SceneColor", out sceneExtent)) &&
                 sceneExtent is { Width: > 0, Height: > 0 })
        {
            _camera.SetAspectRatio(sceneExtent.Width, sceneExtent.Height);
        }


        var buffer = _master.DescriptorFactory.GetDefaultUniformBuffer(frameIndex);
        if (!buffer.IsValid || !buffer.HostVisible)
            return;

        // System.Numerics matrices are row-major/row-vector, which maps directly onto the
        // column-major mat4 the shader reads, so the matrix is uploaded as-is.
        var cameraData = new CameraUboData
        {
            ViewProjection = _camera.GetViewProjectionMatrix()
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
        if (!_geometryPipeline.IsValid)
        {
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
                    VertexAttribute.Create<Vertex>(1, Format.R32G32B32Sfloat, nameof(Vertex.Color)),
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
            };

            _geometryPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
        }

        if (!_cubeMesh.IsValid)
        {
            _cubeMesh = _meshFactory.CreateStatic(new Mesh(TestMeshes.CubeVertices, TestMeshes.CubeIndices));
        }
    }

    public void Dispose()
    {
        if (_cubeMesh.IsValid)
        {
            _meshFactory.Destroy(_cubeMesh);
            _cubeMesh = default;
        }

        CurrentSubmissions = Array.Empty<DrawSubmission>();
    }
}



internal class MeshRenderer : Component
{
    
    public Mesh Mesh;
    public DrawSubmission _drawSubmission;

    public override void NewFrame()
    {
        
        
    }
}
