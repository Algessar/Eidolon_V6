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

    private Scene _scene;
    
    private ImGuiRenderer? _imguiRenderer;
    private GameViewRenderer? _gameViewRenderer;
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


    
    //NOTE: TEMP (??)
    private DrawData BuildDrawData(SwapchainHandler swapchain)
    {
        //Descriptor

        var layout = DescriptorFactory.Layout;
        
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
        var pipelineData = PipelineFactory.GetOrCreate(pipelineKey);
        swapchain.CreateFramebuffers(pipelineData.RenderPass, pipelineData.HasDepth);

        var descriptorSet = DescriptorFactory.GetDescriptorSet(0);
        
        return new DrawData
        {
            PipelineData = pipelineData,
            
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
                    ModelMatrix = Matrix4x4.Identity,
                    PushConstants = PushConstantPayload.Empty
                }
            ]
        };
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