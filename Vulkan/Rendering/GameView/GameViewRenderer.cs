using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace EidolonEngine;

internal class GameViewRenderer : IDisposable
{
    private VulkanMaster _master;
    
    private DrawSubmission[] _drawSubmissions = Array.Empty<DrawSubmission>();
    public DrawSubmission[] DrawSubmissions => _drawSubmissions;

    private PipelineData _pipelineData;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorSet _descriptorSet;

    private GpuBuffer _buffer;
    

    public GameViewRenderer(VulkanMaster master)
    {
        _master = master;
    }
    
    private void Initialize()
    {
        var descriptorLayout = _master.DescriptorFactory.CreateDescriptorSetLayout();

        var descriptorKey = _master.DescriptorFactory.GetDefaultKey();

        var renderPassKey = CreateRenderPassKey(Format.B8G8R8A8Unorm, true);


        var descriptorSet = _master.DescriptorFactory.GetOrCreate(descriptorKey);
       


    }

    private RenderPassKey CreateRenderPassKey(Format colorFormat, bool hasDepth)
    {
        return new RenderPassKey
        {
            ColorFormat = colorFormat,
            HasAlpha =  true,
            HasDepth = hasDepth,
            HasStencil = false,
            LoadOp = AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.ColorAttachmentOptimal,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
            InitialDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };
    }
    
    private PipelineKey CreatePipelineKey(RenderPass renderPass)
    {
        return new PipelineKey
        {
            VertexShaderPath = "basic.vert.spv",
            FragmentShaderPath = "basic.frag.spv",
            RenderPass = renderPass,
            VertexFormat = new VertexFormat
            {
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                Attributes =
                [
                    new VertexAttribute(0, Format.R32G32B32Sfloat, 0),  // Position
                    new VertexAttribute(1, Format.R32G32B32Sfloat, 12), // Normal
                    new VertexAttribute(2, Format.R32G32Sfloat, 24)     // UV
                ]
            },
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.Back,  // Backface culling for 3D
            FrontFace = FrontFace.CounterClockwise,  // Standard for right-handed coords
            HasDepth = true,
            DepthTestEnable = true,   // ✅ Enable depth testing
            DepthWriteEnable = true,  // ✅ Write to depth buffer
            EnableBlending = false,   // Usually no blending for opaque geometry
            BlendState = BlendState.NoBlending,
        };
    }    

    private DrawData BuildDrawData(PipelineData pipelineData, DescriptorSet descriptorSet, DrawSubmission[] submissions)
    {
        return new DrawData
        {
            PipelineData = pipelineData,
            Submissions = submissions,
        };
    }

    private unsafe DrawSubmission BuildDrawSubmission(PipelineData pipelineData, DescriptorSet descriptorSet)
    {

        
        var vertexBuffer = _master.GpuBufferFactory.GetOrCreate();
        
        return new DrawSubmission
        {
            PassType = RenderPassType.GameView,
            PipelineData = pipelineData,
            DescriptorSet = descriptorSet,
            VertexBuffer = _buffer,
            VertexOffset = 0,
            IndexBuffer = default,
            IndexOffset = 0,
            IndexType = IndexType.Uint16,
            VertexCount = 0,
            IndexCount = 0,
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
        };
    }


    public void Dispose()
    {
        // TODO release managed resources here
    }
}