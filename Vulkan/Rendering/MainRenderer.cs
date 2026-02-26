using System.Numerics;
using Eidolon.Vulkan;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace EidolonEngine;

internal class MainRenderer
{
	private VulkanMaster _master;
	
	private readonly CompiledRenderGraph? _initialGraph;

    private ImGuiRenderer _imguiRenderer;
    private SceneRenderer _sceneRenderer;

    public DrawSubmission[] CurrentSubmissions;
    private DrawSubmission[] _sceneSubmissions;

    private DrawData _drawData;

    /*
	 *
     */

    
    public MainRenderer(VulkanMaster master)
    {
	    Debug.Log("Initializing MainRenderer", VALIDATION_LAYERS.WARNING);

	    _master = master;

	    Debug.Log("Successfully initialized MainRenderer", VALIDATION_LAYERS.INFO);

    }

    public void CreateRenderers(CompiledRenderGraph? initialGraph = null)
    {
	    
	    ImGui.CreateContext();
	    _drawData = BuildDrawData(_master.SwapchainHandler);

	    
	    _sceneSubmissions = _drawData.Submissions ?? Array.Empty<DrawSubmission>();
		
	    if (initialGraph is not null)
	    {
		    // Render-graph passes own their framebuffer formats; keep base demo submissions disabled
		    // until scene pipelines are authored per-pass.
		    _sceneSubmissions = Array.Empty<DrawSubmission>();
	    }
        
	    if (_drawData.PipelineData.RenderPass.Handle == 0)
	    {
		    throw new InvalidOperationException("Main pipeline render pass is null before ImGui initialization.");
	    }
	    
	    _imguiRenderer = new ImGuiRenderer(_master);
	    _sceneRenderer = new SceneRenderer();
	    
	    _imguiRenderer.Initialize(_drawData.PipelineData.RenderPass);
	    
	    if (initialGraph is not null)
	    {
		    // Render-graph passes own their framebuffer formats; keep base demo submissions disabled
		    // until scene pipelines are authored per-pass.
		    _master.FrameHandler.SetCompiledGraph(initialGraph);
	    }
    }
	

    public void Run(IWindow window)
    {
        // Keep _window.Load in VulkanMaster for Vulkan setup.

        window.Render += delta =>
        {
	        if (_master.FrameHandler is null)
	        {
		        return;
	        }

	        _imguiRenderer?.NewFrame(
		        (float)delta,
		        new Vector2(window.Size.X, window.Size.Y),
		        new Vector2(window.FramebufferSize.X, window.FramebufferSize.Y));

	        _imguiRenderer?.BuildDrawSubmissions(_master.FrameHandler.CurrentFrame, Constants.MAX_FRAMES_IN_FLIGHT);

	        var baseSubmissions = _sceneSubmissions; // ← reset to original scene submissions //NOTE: Not sure why I need to do that but sure.  

	        var sceneSubmissions = _sceneRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
	        var uiSubmissions = _imguiRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();

	        var mergedSubmissions = new DrawSubmission[
		        baseSubmissions.Length + // reset added here, still not sure why
	            uiSubmissions.Length + // UI rendering
	            sceneSubmissions.Length]; // Game view rendering (geometry etc)
	        
	        baseSubmissions.CopyTo(mergedSubmissions, 0);
	        uiSubmissions.CopyTo(mergedSubmissions, baseSubmissions.Length);

	        _drawData.Submissions = mergedSubmissions;

	        _master.FrameHandler.Draw(in _drawData);
        };
        
        window.Run();
    }
    
    private DrawData BuildDrawData(SwapchainHandler swapchain)
    {
        //Descriptor

        var layout = _master.DescriptorFactory.Layout;
        
        //RenderPass
        
        var renderPassKey = new RenderPassKey
        {
            ColorFormat = swapchain.SwapchainImageFormat,
            DepthFormat = Format.D32Sfloat,
            HasDepth = false,
            HasAlpha = true,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
            InitialDepthLayout = ImageLayout.Undefined,
            FinalDepthLayout =  ImageLayout.DepthStencilAttachmentOptimal,
        };
        
        var renderPass = _master.RenderPassFactory.CreateRenderPass(renderPassKey);
        
        //Pipeline
        
        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = "basic.vert.spv",
            FragmentShaderPath = "basic.frag.spv",
            RenderPass = renderPass,
            Layout = layout,
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
            EnableBlending = false,   // Usually no blending for opaque geometry
            BlendState = BlendState.NoBlending,
        };
        var pipelineData = _master.PipelineFactory.GetOrCreate(pipelineKey);
        swapchain.CreateFramebuffers(pipelineData.RenderPass, pipelineData.HasDepth);

        var descriptorSet = _master.DescriptorFactory.GetDescriptorSet(0);
        
        return new DrawData
        {
            PipelineData = pipelineData,
            ModelMatrix = Matrix4x4.Identity,
            Submissions =
            [
                new DrawSubmission
                {
                    PassType = RenderPassType.Geometry,
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
                    PushConstants = PushConstantPayload.Empty
                }
            ]
        };
    }
}

