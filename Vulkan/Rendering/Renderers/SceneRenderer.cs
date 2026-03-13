using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using Silk.NET.Vulkan;

namespace EidolonEngine;

internal class SceneRenderer : IDisposable
{
    
    private readonly VulkanMaster _master;
    private readonly MeshFactory _meshFactory;
    private readonly DescriptorSet _descriptorSet;

    private PipelineData _geometryPipeline;
    private MeshHandle _triangleMesh;
     
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    public SceneRenderer(VulkanMaster master, MeshFactory meshFactory)
    {
        _master = master;
        _meshFactory = meshFactory;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(0);
        
    }
    
    // Create and add geometry to scenes

    // Build submissions

    // PipelineKeys are individual depending on shaders

    public void NewFrame()
    {
        
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

        CurrentSubmissions =
        [
            new DrawSubmission
            {
                PassType = RenderPassType.Geometry,
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

    private void EnsureGeometryResources()
    {
        if (!_geometryPipeline.IsValid)
        {
            var renderPassKey = new RenderPassKey
            {
                ColorFormat = Format.R16G16B16A16Sfloat,
                DepthFormat = Format.D32Sfloat,
                HasDepth = false,
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
                HasDepth = false,
                DepthTestEnable = false,
                DepthWriteEnable = false,
                EnableBlending = false,
                BlendState = BlendState.NoBlending,
            };

            _geometryPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
        }

        if (!_triangleMesh.IsValid)
        {
            var vertices = new[]
            {
                new Vertex(new Vector3(0f, -0.6f, 0f), new Vector3(1f, 0.3f, 0.3f)),
                new Vertex(new Vector3(0.6f, 0.6f, 0f), new Vector3(0.3f, 1f, 0.3f)),
                new Vertex(new Vector3(-0.6f, 0.6f, 0f), new Vector3(0.3f, 0.5f, 1f)),
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