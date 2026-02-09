using System.Numerics;
using System.Runtime.InteropServices;
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
    // public Surfaces Surfaces { get; set; }
    public KhrSurface KhrSurface { get; set; }
    public SurfaceKHR SurfaceKhr { get; set; }

    #endregion

    #region Managers/Factories

    public ShaderManager ShaderManager { get; set; }
    public BufferFactory BufferFactory { get; set; }
    public DescriptorFactory DescriptorFactory { get; set; }
    public RenderPassFactory RenderPassFactory { get; set; }
    public PipelineFactory PipelineFactory { get; set; }
    public CommandManager CommandManager { get; set; }

    #endregion
    
    public SwapchainHandler SwapchainHandler { get; private set; }
    public FrameHandler FrameHandler { get; private set; }

    private readonly CompiledRenderGraph? _initialGraph;
    DrawData _drawData;
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

            VulkanDevice = new VulkanDevice(this);
            
            SwapchainHandler = new SwapchainHandler(this, SurfaceKhr, KhrSurface);
            
            InitializeManagers();
            SetupFrameChain();

        };

        _window.Render += (double delta) =>
        {
                        
            if (FrameHandler is null)
            {
                return;
            }

            try
            {
                // var drawData = default(DrawData);
                FrameHandler.BeginFrame(in _drawData);
                FrameHandler.Draw(in _drawData);
                FrameHandler.EndFrame(in _drawData);
            }
            catch (Exception ex)
            {
                Debug.Log($"Frame loop error: {ex.Message}", VALIDATION_LAYERS.ERROR);
            }
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
        FrameHandler = new FrameHandler(this);
        PipelineFactory = new PipelineFactory(this);

        FrameHandler.Initialize();
        
    }

    private void SetupFrameChain()
    {
        var id = ImGui.CreateContext(); //NOTE: Had missed completely that CreateContext() returns an ID.

        _drawData = BuildDrawData(SwapchainHandler);
        
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

    //NOTE: TEMP
    private DrawData BuildDrawData(SwapchainHandler swapchain)
    {
        //Descriptor
        
        var descriptorSet = DescriptorFactory.CreateDescriptorSet();
        
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
        
        var renderPass = RenderPassFactory.CreateRenderPass(renderPassKey);
        
        //Pipeline
        
        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = "basic.vert.spv",
            FragmentShaderPath = "basic.frag.spv",
            RenderPass = renderPass,
            Layout = DescriptorFactory.CreateDescriptorSetLayout(),
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
        
        var pipelineData = PipelineFactory.GetOrCreate(pipelineKey);
        
        return new DrawData
        {
            PipelineData = pipelineData,
            DescriptorSet = descriptorSet,
            ModelMatrix = Matrix4x4.Identity,
            
            
        };
    }
    
    
    public void Dispose()
    {
        Vk.Dispose();
        VulkanDevice.Dispose();
        DescriptorFactory.Dispose();
        CommandManager.Dispose();
    }
}