using Eidolon.Vulkan.Rendering;
using EidolonEngine;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Simulation;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal class VulkanMaster : IDisposable
{
    #region Vulkan Core

    public Vk Vk;
    
    private readonly IWindow _window;
    
    //NOTE: Possibly temp
    public IWindow GetWindow => _window;
    
    public VulkanInstance VulkanInstance { get; set; }
    public VulkanDevice VulkanDevice { get; set; }
    public KhrSurface KhrSurface { get; set; }
    public SurfaceKHR SurfaceKhr { get; set; }

    #endregion

    #region Managers/Factories

    public ShaderManager ShaderManager { get; set; }
    public GpuBufferFactory GpuBufferFactory { get; set; }
    public DescriptorFactory DescriptorFactory { get; set; }
    public RenderPassFactory RenderPassFactory { get; set; }
    public PipelineFactory PipelineFactory { get; set; }
    public CommandHandler CommandHandler { get; set; }

    #endregion
    
    public SwapchainHandler SwapchainHandler { get; private set; }

    #region Rendering
   
    public FrameHandler? FrameHandler { get; set; }

    private readonly MainRenderer _mainRenderer;
    private readonly SceneSession _session;
    private readonly AssetLibrary _assets;

    private bool _initialized;
    private bool _disposed;
    #endregion

    /// <summary>The world currently being rendered: the runtime copy during play, else authored.</summary>
    public World World => _session.Active;

    /// <summary>Owns the authored world and, while play mode runs, the runtime copy.</summary>
    public SceneSession Session => _session;

    public VulkanMaster(SceneSession session, AssetLibrary assets, CompiledRenderGraph? initialGraph = null)
    {
        _session = session;
        _assets = assets;

        // Compile in place in the deployed shader folder so this run picks up the result.
        ShaderCompiler.CompileShaders(Path.Combine(AppContext.BaseDirectory, "Rendering", "Shaders"));
        
        Debug.Log("::: Initializing Vulkan resources :::", VALIDATION_LAYERS.WARNING);

        Vk = Vk.GetApi();
        
        //TODO: Consider moving window handling out of Vulkan and into Editor.
        
        _window = CreateOSWindow();

        _mainRenderer = new MainRenderer(this);
        _window.Load += () =>
        {
            InitializeManagers();
            _mainRenderer.CreateRenderers(_session, _assets, initialGraph);
        };
        
        _mainRenderer.Run(_window);
    }

    private void InitializeManagers()
    {
        VulkanInstance = new VulkanInstance(this, _window);
            
        KhrSurface = VulkanInstance.KhrSurface;
        SurfaceKhr = VulkanInstance.SurfaceKhr;

        VulkanDevice = new VulkanDevice(this);
            
        SwapchainHandler = new SwapchainHandler(this, SurfaceKhr, KhrSurface);
        ShaderManager = new ShaderManager(this);
        GpuBufferFactory = new GpuBufferFactory(this);
        DescriptorFactory = new DescriptorFactory(this);
        RenderPassFactory = new RenderPassFactory(this);
        CommandHandler = new CommandHandler(this);
        FrameHandler = new FrameHandler(this);
        PipelineFactory = new PipelineFactory(this);
        _initialized = true;
        Debug.Log("Vulkan resources initialized.", VALIDATION_LAYERS.SUCCESS);
        
    }

    private IWindow CreateOSWindow()
    {
        var opts = WindowOptions.DefaultVulkan with
        {
            Title = "Eidolon Editor",
            Size = new Vector2D<int>(900, 720),
            ShouldSwapAutomatically = true,
            VSync = true,
            WindowBorder = WindowBorder.Resizable,
            WindowState = WindowState.Maximized
        };
        
        return Window.Create(opts);
    }
    
    /// <summary>
    /// Tears the backend down in dependency order. Everything that belongs to the device has
    /// to go before the device itself, and the swapchain before the surface it presents to.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_initialized)
        {
            Vk?.Dispose();
            return;
        }

        Debug.Log("Disposing VulkanMaster", VALIDATION_LAYERS.WARNING);

        var device = VulkanDevice.Device;
        Vk.DeviceWaitIdle(device);

        // Renderer-owned resources (mesh buffers, font atlas, UI geometry, descriptor pool).
        _mainRenderer.Dispose();

        // Per-frame resources: graph images, framebuffers, sync objects, command pools.
        FrameHandler?.Dispose();
        CommandHandler.Dispose();

        // Pipelines reference shader modules and render passes, so they go first.
        PipelineFactory.Dispose();
        ShaderManager.Dispose();
        DescriptorFactory.Dispose();
        GpuBufferFactory.Dispose();
        RenderPassFactory.Dispose();

        // The swapchain must be destroyed before the surface.
        SwapchainHandler.Dispose();

        VulkanDevice.Dispose();
        VulkanInstance.Dispose();
        Vk.Dispose();

        Debug.Log("Disposed VulkanMaster", VALIDATION_LAYERS.INFO);
    }
}