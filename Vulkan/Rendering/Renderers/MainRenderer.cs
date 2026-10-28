using System.Numerics;
using Eidolon.Editor;
using Eidolon.Engine;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal class MainRenderer : IDisposable
{
	private VulkanMaster _master;
	private InputManager _inputManager;
	private MeshFactory _meshFactory;
	
    private ImGuiRenderer _imguiRenderer;
    private SceneRenderer _sceneRenderer;
    private GridRenderer? _gridRenderer;
    private ViewportSettings? _viewportSettings;

    private PipelineData _bootstrapPipelineData;

    public MainRenderer(VulkanMaster master)
    {
	    Debug.Log("Initializing MainRenderer", VALIDATION_LAYERS.WARNING);

	    _master = master;

	    Debug.Log("Successfully initialized MainRenderer", VALIDATION_LAYERS.INFO);

    }

    public void CreateRenderers(SceneSession session, AssetLibrary assets, CompiledRenderGraph? initialGraph = null)
    {
	    
	    ImGui.CreateContext();
	    _bootstrapPipelineData = BuildBootstrapPipelineData(_master.SwapchainHandler);

	    if (_bootstrapPipelineData.RenderPass.Handle == 0)
	    {
		    throw new InvalidOperationException("Main pipeline render pass is null before ImGui initialization.");
	    }
	    
	    _inputManager = new InputManager(_master.GetWindow);
	    _meshFactory = new MeshFactory(_master);
	    _viewportSettings = _master.Viewport;
        _gridRenderer = new GridRenderer(_master, _viewportSettings);
        _imguiRenderer = new ImGuiRenderer(_master, _inputManager, _master.Editor);
	    _sceneRenderer = new SceneRenderer(_master, _meshFactory, session);
	    
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
	        // Simulation only advances while play mode is running.
	        _master.Session.Tick((float)delta);

	        _sceneRenderer?.NewFrame();
	        _imguiRenderer?.NewFrame(
		        (float)delta,
		        new Vector2(window.Size.X, window.Size.Y),
		        new Vector2(window.FramebufferSize.X, window.FramebufferSize.Y));
	        // The editor UI knows how big the game view panel ended up this frame.
	        if (_imguiRenderer is not null)
	        {
		        _sceneRenderer?.SetViewSizes(_imguiRenderer.EditorViewSize, _imguiRenderer.GameViewSize);

		        // Built after the UI so the grid follows any viewport changes made this frame.
		        _gridRenderer?.NewFrame();
	        }
        };
        
        window.Render += delta =>
        {
	        if (_master.FrameHandler is null)
	        {
		        return;
	        }

	        var sceneSubmissions = _sceneRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
	        var gridSubmissions = _gridRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
	        var uiSubmissions = _imguiRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();

	        var mergedSubmissions = new DrawSubmission[sceneSubmissions.Length + gridSubmissions.Length + uiSubmissions.Length];
	        sceneSubmissions.CopyTo(mergedSubmissions, 0);
	        gridSubmissions.CopyTo(mergedSubmissions, sceneSubmissions.Length);
	        uiSubmissions.CopyTo(mergedSubmissions, sceneSubmissions.Length + gridSubmissions.Length);

	        var drawData = new DrawData { Submissions = mergedSubmissions };

	        _master.FrameHandler.Draw(in drawData);
        };
        
        window.Run();
    }

    /// <summary>
    /// Releases the renderer-owned GPU resources. Called from <see cref="VulkanMaster.Dispose"/>
    /// while the device is still alive.
    /// </summary>
    public void Dispose()
    {
        _imguiRenderer?.Dispose();
        _gridRenderer?.Dispose();
        _sceneRenderer?.Dispose();
        _meshFactory?.Dispose();
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
}

