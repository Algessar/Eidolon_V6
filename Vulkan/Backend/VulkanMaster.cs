using Eidolon.Vulkan.Rendering;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal class VulkanMaster
{
    #region Vulkan Core

    public Vk Vk;
    
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

    public VulkanMaster()
    {
        Debug.Log("::: Initializing Vulkan resources :::", VALIDATION_LAYERS.WARNING);
        Vk = Vk.GetApi();
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
        };

        _window.Render += (double delta) =>
        {
            
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
        var swapchain = new SwapchainHandler(this, SurfaceKhr, KhrSurface);
        
        var frameHandler = new FrameHandler(this, swapchain);
        
        frameHandler.Initialize();
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



}