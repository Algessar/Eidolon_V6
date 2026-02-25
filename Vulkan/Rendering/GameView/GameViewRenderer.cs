using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace EidolonEngine;

internal class GameViewRenderer : IDisposable
{
    
    private DrawSubmission[] _drawSubmissions = Array.Empty<DrawSubmission>();
    public DrawSubmission[] DrawSubmissions => _drawSubmissions;
   

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
    
    private PipelineKey DefinePipelineKey(RenderPass renderPass, DescriptorSetLayout layout,
        string vertexShaderPath, string fragmentShaderPath, VertexFormat vertexFormat)
    {
        return new PipelineKey
        {
            VertexShaderPath = vertexShaderPath,
            FragmentShaderPath = fragmentShaderPath,
            RenderPass = renderPass,
            Layout = layout,
            VertexFormat = vertexFormat,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.Back,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
        };
    }

    private DrawData BuildDrawData(PipelineData pipelineData, DescriptorSet descriptorSet, DrawSubmission[] submissions)
    {
        return new DrawData
        {
            PipelineData = pipelineData,
            ModelMatrix = null,
            Submissions = submissions,
        };
    }

    private DrawSubmission BuildDrawSubmission(PipelineData pipelineData, DescriptorSet descriptorSet)
    {
        return new DrawSubmission
        {
            PassType = RenderPassType.GameView,
            PipelineData = pipelineData,
            DescriptorSet = descriptorSet,
            VertexBuffer = default,
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
        };
    }


    public void Dispose()
    {
        // TODO release managed resources here
    }
}