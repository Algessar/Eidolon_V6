using Eidolon.Vulkan.Rendering;
using EidolonCore.Rendering;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal class VulkanMaster : IRenderer
{
    #region Vulkan Core

    public Vk Vk;
    
    public static VulkanMaster Instance { get; private set; }
    
    private IWindow _window;
    
    //NOTE: Possibly temp
    public IWindow GetWindow => _window;
    
    public VulkanInstance VulkanInstance { get; set; }
    public VulkanDevice VulkanDevice { get; set; }
    public Surfaces Surfaces { get; set; }
    public KhrSurface KhrSurface { get; set; }
    public SurfaceKHR SurfaceKhr { get; set; }

    #endregion

    #region Managers

    public ShaderManager ShaderManager { get; set; }
    public BufferFactory BufferFactory { get; set; }
    public DescriptorFactory DescriptorFactory { get; set; }
    public RenderPassFactory RenderPassFactory { get; set; }
    public CommandManager CommandManager { get; set; }

    #endregion
    
    public FrameHandler FrameHandler { get; private set; }

    private readonly CompiledRenderGraph? _initialGraph;
    public VulkanMaster(CompiledRenderGraph? initialGraph = null)
    {
        _initialGraph = initialGraph;
        Debug.Log("::: Initializing Vulkan resources :::", VALIDATION_LAYERS.WARNING);
        Vk = Vk.GetApi();
        
        //NOTE: Window creation should probably not be done here in the end?
        CreateOSWindow();

        if (_window == null)
        {
            Debug.Log("Window is null.", VALIDATION_LAYERS.ERROR);
        }
        
        _window.Load += () =>
        {
            VulkanInstance = new VulkanInstance(this, _window);
            
            KhrSurface = VulkanInstance.KhrSurface;
            SurfaceKhr = VulkanInstance.SurfaceKhr;

            //TODO: Check if this works now.
            Surfaces = new Surfaces
            {
                KhrSurface = VulkanInstance.KhrSurface,
                SurfaceKhr = VulkanInstance.SurfaceKhr,
            };
            
            VulkanDevice = new VulkanDevice(this);
            
            InitializeManagers();
            SetupFrameChain();
        };
        
        var drawData = new DrawData();

        _window.Render += (double delta) =>
        {
            FrameHandler.Draw(drawData);
        };
        
        _window.Run();
        
        Debug.Log("Vulkan resources initialized.", VALIDATION_LAYERS.SUCCESS);
    }

    public void InitializeManagers()
    {
        ShaderManager = new ShaderManager(this);
        BufferFactory = new BufferFactory(this);
        DescriptorFactory = new DescriptorFactory(this);
        RenderPassFactory = new RenderPassFactory(this);
        CommandManager = new CommandManager(this);
        
    }

    private void SetupFrameChain()
    {
        var id = ImGui.CreateContext(); //NOTE: Had missed completely that CreateContext() returns an ID.
        var swapchain = new SwapchainHandler(this, SurfaceKhr, KhrSurface);
        
        FrameHandler = new FrameHandler(this, swapchain);
        
        FrameHandler.Initialize();
        
        if (_initialGraph is not null)
        {
            FrameHandler.SetCompiledGraph(_initialGraph);
        }

    }

    private void CreateOSWindow()
    {
        var opts = WindowOptions.DefaultVulkan with
        {
            Title = "Eidolon Editor",
            Size = new Vector2D<int>(900, 720),
            ShouldSwapAutomatically = true,
            VSync = true,
            WindowBorder = WindowBorder.Resizable,
        };
        
        _window = Window.Create(opts);
    }
    
    
    public void Dispose()
    {
        Vk.Dispose();
        VulkanDevice.Dispose();
        DescriptorFactory.Dispose();
        CommandManager.Dispose();
    }
}