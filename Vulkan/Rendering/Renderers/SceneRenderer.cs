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
    
    // private readonly DescriptorSet _descriptorSet;
    private PipelineData _geometryPipeline;
    private MeshHandle _triangleMesh;
     
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    public SceneRenderer(VulkanMaster master, MeshFactory meshFactory)
    {
        _master = master;
        _meshFactory = meshFactory;

        _camera = new Camera
        {
            Position = new Vector3(0, 0, 10f)
        };
        _camera.SetTarget(Vector3.Zero);
        Debug.Log($"SCENE RENDERER: CameraPos: {_camera.Position} :: Camera Target: {_camera.Target}", VALIDATION_LAYERS.INFO);

    }
    
    public void NewFrame()
    {
        BuildDrawSubmissions();
        // Debug.Log($"DrawSubmission count: {CurrentSubmissions.Length}");
        Debug.Log($"Camera pos: {_camera.Position}", VALIDATION_LAYERS.INFO);
    }

   public void BuildDrawSubmissions()
    {
        EnsureGeometryResources();

        if (!_geometryPipeline.IsValid)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        if (!_meshFactory.TryGet(_triangleMesh, out var mesh))
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }
        
        var frameIndex = _master.FrameHandler?.CurrentFrame ?? 0;
        var descriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex);
        UpdateCameraUniformBuffer(frameIndex);

        Debug.Log($"Mesh vertex count: {mesh.VertexCount}");
        Debug.Log($"Mesh index count: {mesh.IndexCount}");

        Debug.Log($"Mesh VertexBuffer: {mesh.VertexBuffer.Buffer.Handle}", VALIDATION_LAYERS.INFO);
        Debug.Log($"Mesh IndexBuffer: {mesh.IndexBuffer.Buffer.Handle}", VALIDATION_LAYERS.INFO);

        
        CurrentSubmissions =
        [
            new DrawSubmission
            {
                PassType = RenderPassType.Geometry,
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
                PushConstants = PushConstantPayload.Empty,
                ModelMatrix = Matrix4x4.Identity,
            }
        ];
    }


    private void UpdateCameraUniformBuffer(uint frameIndex)
    {
        if (_master.FrameHandler is { } frameHandler &&
            frameHandler.TryGetResourceExtent("SceneColor", out var sceneExtent) &&
            sceneExtent is { Width: > 0, Height: > 0 })
        {
            _camera.SetAspectRatio(sceneExtent.Width, sceneExtent.Height);
        }

        var buffer = _master.DescriptorFactory.GetDefaultUniformBuffer(frameIndex);
        if (!buffer.IsValid || !buffer.HostVisible)
            return;

        var cameraData = new CameraUboData
        {
            ViewProjection = Matrix4x4.Transpose(_camera.GetViewProjectionMatrix())
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
                // Attributes = Array.Empty<VertexAttribute>()
                Attributes =
                [
                    VertexAttribute.Create<Vertex>(0, Format.R32G32B32Sfloat, nameof(Vertex.Position)),
                    VertexAttribute.Create<Vertex>(1, Format.R32G32B32Sfloat, nameof(Vertex.Color)),
                ]
            };

            var pipelineKey = new PipelineKey
            {
                // VertexShaderPath = "basic.vert.spv",
                // FragmentShaderPath = "basic.frag.spv",
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
                DepthWriteEnable = false,
                EnableBlending = false,
                BlendState = BlendState.AlphaBlending,
            };

            _geometryPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
        }

        if (!_triangleMesh.IsValid)
        {
            var vertices = new[]
            {
                new Vertex(new Vector3(0f, -0.5f, 0f), new Vector3   (1.0f, 0.2f, 0.2f)),
                new Vertex(new Vector3(0.5f, 0.5f, 0f), new Vector3  (0.2f, 1.0f, 0.3f)),
                new Vertex(new Vector3(-0.5f, 0.5f, 0f), new Vector3 (0.2f, 0.5f, 1.0f)),
            };
            var indices = new uint[] { 0, 1, 2 };

            _triangleMesh = _meshFactory.CreateStatic(new Mesh(vertices, indices));
        }
    }

    public void Dispose()
    {
        if (_triangleMesh.IsValid)
        {
            _meshFactory.Destroy(_triangleMesh);
            _triangleMesh = default;
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