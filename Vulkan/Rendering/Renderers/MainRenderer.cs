using System.Numerics;
using Eidolon.Editor;
using Eidolon.Engine;
using Eidolon.Vulkan;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace EidolonEngine;

internal class MainRenderer
{
	private VulkanMaster _master;
	private InputManager _inputManager;
	private CameraController? _cameraController;

	private IInputContext? _input;
	
	private readonly CompiledRenderGraph? _initialGraph;

    private ImGuiRenderer _imguiRenderer;
    private SceneRenderer _sceneRenderer;
    private GameViewRenderer _gameViewRenderer;

    private DrawSubmission[] _sceneSubmissions;

    private DrawData _drawData;
    
    private PipelineData _bootstrapPipelineData;

    public MainRenderer(VulkanMaster master)
    {
	    Debug.Log("Initializing MainRenderer", VALIDATION_LAYERS.WARNING);

	    _master = master;

	    Debug.Log("Successfully initialized MainRenderer", VALIDATION_LAYERS.INFO);

    }

    public void CreateRenderers(CompiledRenderGraph? initialGraph = null)
    {
	    
	    ImGui.CreateContext();
	    _bootstrapPipelineData = BuildBootstrapPipelineData(_master.SwapchainHandler);
	    _drawData = BuildInitialDrawData(_bootstrapPipelineData);
	    _sceneSubmissions = _drawData.Submissions ?? Array.Empty<DrawSubmission>();
		
	    if (initialGraph is not null)
	    {
		    // Render-graph passes own their framebuffer formats; keep base demo submissions disabled
		    // until scene pipelines are authored per-pass.
		    _sceneSubmissions = Array.Empty<DrawSubmission>();
	    }
        
	    if (_bootstrapPipelineData.RenderPass.Handle == 0)
	    {
		    throw new InvalidOperationException("Main pipeline render pass is null before ImGui initialization.");
	    }
	    
	    _inputManager = new InputManager(_master.GetWindow);
	    _imguiRenderer = new ImGuiRenderer(_master, _inputManager);
	    _sceneRenderer = new SceneRenderer(_master);
	    _gameViewRenderer = new GameViewRenderer(_master);
	    _cameraController = new CameraController(_inputManager.Input);
	    
	    _imguiRenderer.Initialize(_bootstrapPipelineData.RenderPass);
	    
	    if (initialGraph is not null)
	    {
		    // Render-graph passes own their framebuffer formats; keep base demo submissions disabled
		    // until scene pipelines are authored per-pass.
		    _master.FrameHandler?.SetCompiledGraph(initialGraph);
	    }
    }
	

    public void Run(IWindow window)
    {
        // Keep _window.Load in VulkanMaster for Vulkan setup.

        window.Update += (delta) =>
        {
	        _sceneRenderer?.NewFrame();
	        _gameViewRenderer?.NewFrame(delta);
	        _imguiRenderer?.NewFrame(
		        (float)delta,
		        new Vector2(window.Size.X, window.Size.Y),
		        new Vector2(window.FramebufferSize.X, window.FramebufferSize.Y));
	        
	        //NOTE: This annoys the fuck out of me. What is CurrentFrame doing here?
	        // _imguiRenderer?.BuildDrawSubmissions(_master.FrameHandler.CurrentFrame, Constants.MAX_FRAMES_IN_FLIGHT);
	        if (_cameraController is not null)
	        {
		        // Debug.Log($"Camera controller is not null");
		        _cameraController.Update((float)delta);
		        _cameraController.SetAspect(_imguiRenderer.GameViewSize);
	        }
        };
        
        window.Render += delta =>
        {
	        if (_master.FrameHandler is null)
	        {
		        return;
	        }

	        var baseSubmissions = Array.Empty<DrawSubmission>(); 

	        var sceneSubmissions = _sceneRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
	        var gameViewSubmissions = _gameViewRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
	        var uiSubmissions = _imguiRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();

	        var mergedSubmissions = new DrawSubmission[
		        baseSubmissions.Length +
		        sceneSubmissions.Length +
		        gameViewSubmissions.Length +
		        uiSubmissions.Length];

	        baseSubmissions.CopyTo(mergedSubmissions, 0);
	        sceneSubmissions.CopyTo(mergedSubmissions, baseSubmissions.Length);
	        
	        gameViewSubmissions.CopyTo(mergedSubmissions, baseSubmissions.Length + sceneSubmissions.Length);
	        uiSubmissions.CopyTo(mergedSubmissions, baseSubmissions.Length + sceneSubmissions.Length + gameViewSubmissions.Length);

	        _drawData.Submissions = mergedSubmissions;

	        _master.FrameHandler.Draw(in _drawData);
        };
        
        window.Run();
    }
    
    private PipelineData BuildBootstrapPipelineData(SwapchainHandler swapchain)
    {
	    var layout = _master.DescriptorFactory.Layout;

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
		    EnableBlending = false,
		    BlendState = BlendState.NoBlending,
	    };

	    return _master.PipelineFactory.GetOrCreate(pipelineKey);
    }

    
    private DrawData BuildInitialDrawData(PipelineData pipelineData)
    {
	    var descriptorSet = _master.DescriptorFactory.GetDescriptorSet(0);

	    return new DrawData
	    {
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
				    PushConstants = PushConstantPayload.Empty,
				    ModelMatrix = Matrix4x4.Identity,
			    }
		    ]
	    };
    }
}

