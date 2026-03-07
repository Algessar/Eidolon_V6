using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using ImGuiNET;
using Silk.NET.Vulkan;

namespace EidolonEngine;

/// <summary>
/// Produces game-view draw submissions. Render-pass/framebuffer ownership stays in render-graph execution.
/// </summary>
internal sealed class GameViewRenderer
{
    private VulkanMaster _master;

    

    private readonly DescriptorSet _descriptorSet;
    private PipelineData _gameViewPipeline;
    
    private GpuBuffer _gridVertexBuffer;
    private GpuBufferKey _gridVertexBufferKey;
    private uint _gridVertexCount;


    public GameViewRenderer(VulkanMaster master)
    {
        _master = master;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(0);
    }
    
    
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();
    public void NewFrame(double delta)
    {
        BuildDrawSubmissions();
        // Debug.Log("Running NewFrame in GameViewRenderer", VALIDATION_LAYERS.INFO);
    }

    public void BuildDrawSubmissions()
    {
        EnsurePipeline();
        // EnsureGridGeometry();
        CurrentSubmissions = BuildGameViewSubmissions(_gameViewPipeline, _descriptorSet);
        // Debug.Log($"[GameView] Submissions count: {CurrentSubmissions.Length}, VertexCount: {_gridVertexCount}, Pipeline valid: {_gameViewPipeline.IsValid}");
    }

    private void EnsurePipeline()
    {
        if (_gameViewPipeline.IsValid)
            return;

        // Keep this pipeline compatible with the render-graph GameView target format
        // (EidolonEditor builds GameView as Rgba16Float).
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

        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = "basic.vert.spv",
            FragmentShaderPath = "basic.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = new VertexFormat
            {
                Stride = 0,
                Attributes = Array.Empty<VertexAttribute>()
            },
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = false,
            DepthTestEnable = false,
            DepthWriteEnable = false,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
        };

        _gameViewPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
    }

    private DrawSubmission[] BuildGameViewSubmissions(PipelineData pipelineData,  DescriptorSet descriptorSet)
    {
        if (!pipelineData.IsValid)
        {
            return Array.Empty<DrawSubmission>();
        }

        return
        [
            new DrawSubmission
            {
                PassType = RenderPassType.GameView,
                PipelineData = pipelineData,
                DescriptorSet = descriptorSet,
                VertexBuffer = default,
                VertexOffset = 0,
                IndexBuffer = default,
                IndexOffset = 0,
                IndexType = IndexType.Uint16,
                VertexCount = 3,
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
            }
        ];
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}