using System.Numerics;
using Eidolon.Vulkan.Rendering;
using EidolonEngine;
using ImGuiNET;
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
    
    
    private DrawData _drawData;
 
    private MainRenderer? _mainRenderer;
    #endregion


    
    public VulkanMaster(CompiledRenderGraph? initialGraph = null)
    {
        ShaderCompiler.CompileShaders(@"G:\Coding\Eidolon_V6\Vulkan\Rendering\Shaders");
        
        Debug.Log("::: Initializing Vulkan resources :::", VALIDATION_LAYERS.WARNING);

        Vk = Vk.GetApi();
        
        //TODO: Consider moving window handling out of Vulkan and into Editor.
        
        CreateOSWindow();

        _mainRenderer = new MainRenderer(this);
        _window.Load += () =>
        {
            InitializeManagers();
            _mainRenderer.CreateRenderers(initialGraph);
        };
        
        _mainRenderer?.Run(_window);
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
        Debug.Log("Vulkan resources initialized.", VALIDATION_LAYERS.SUCCESS);
        
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
            WindowState = WindowState.Maximized
        };
        
        _window = Window.Create(opts);
    }
    
    private void Dispose()
    {
        Debug.Log("Disposing VulkanMaster", VALIDATION_LAYERS.WARNING);
        
        Vk.Dispose();
        VulkanDevice.Dispose();
        DescriptorFactory.Dispose();
        GpuBufferFactory.Dispose();
        PipelineFactory.Dispose();
        CommandHandler.Dispose();
        
        
        Debug.Log("Disposed VulkanMaster", VALIDATION_LAYERS.INFO);
    }
}