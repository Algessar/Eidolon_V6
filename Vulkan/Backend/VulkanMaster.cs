using System.Numerics;
using Eidolon.Vulkan.Rendering;
using EidolonCore.Rendering;
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
    
    public static VulkanMaster Instance { get; private set; }
    
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
    // public BufferFactory BufferFactory { get; set; }
    public GpuBufferFactory GpuBufferFactory { get; set; }
    public DescriptorFactory DescriptorFactory { get; set; }
    public RenderPassFactory RenderPassFactory { get; set; }
    public PipelineFactory PipelineFactory { get; set; }
    public CommandHandler CommandHandler { get; set; }

    #endregion
    
    public SwapchainHandler SwapchainHandler { get; private set; }
    private FrameHandler FrameHandler { get; set; }

    
    private ImGuiRenderer? _imguiRenderer;
    private readonly CompiledRenderGraph? _initialGraph;
    
    private DrawData _drawData;
    private DrawSubmission[] _sceneSubmissions = Array.Empty<DrawSubmission>();
    
    public VulkanMaster(CompiledRenderGraph? initialGraph = null)
    {
        ShaderCompiler.CompileShaders(@"G:\Coding\Eidolon_V6\Vulkan\Rendering\Shaders");
        
        _initialGraph = initialGraph;
        Debug.Log("::: Initializing Vulkan resources :::", VALIDATION_LAYERS.WARNING);

        Vk = Vk.GetApi();
        
        CreateOSWindow();
        
        _window.Load += () =>
        {
            InitializeManagers();
            SetupFrameChain();
        };

        _window.Render += (double delta) =>
        {
            if (FrameHandler is null)
            {
                return;
            }
            
            _imguiRenderer?.NewFrame((float) delta, new Vector2(_window.Size.X, _window.Size.Y)); // Wonder if my Vec2 works ^^ I doubt it, no implicit operator for Vector2D<T>
            _imguiRenderer?.BuildUI();
            _imguiRenderer?.FinalizeFrame();
  
            
            _imguiRenderer?.BuildDrawSubmissions(FrameHandler.CurrentFrame, Constants.MAX_FRAMES_IN_FLIGHT);

            var baseSubmissions = _sceneSubmissions;   // ← reset to original scene submissions
            var uiSubmissions = _imguiRenderer?.CurrentSubmissions ?? Array.Empty<DrawSubmission>();
            var mergedSubmissions = new DrawSubmission[baseSubmissions.Length + uiSubmissions.Length];
            baseSubmissions.CopyTo(mergedSubmissions, 0);
            uiSubmissions.CopyTo(mergedSubmissions, baseSubmissions.Length);
            _drawData.Submissions = mergedSubmissions;
            FrameHandler.Draw(in _drawData);
        };
        
        _window.Run();
    }

    private void InitializeManagers()
    {
        VulkanInstance = new VulkanInstance(this, _window);
            
        KhrSurface = VulkanInstance.KhrSurface;
        SurfaceKhr = VulkanInstance.SurfaceKhr;

        VulkanDevice = new VulkanDevice(this);
            
        SwapchainHandler = new SwapchainHandler(this, SurfaceKhr, KhrSurface);
        ShaderManager = new ShaderManager(this);
        // BufferFactory = new BufferFactory(this);
        GpuBufferFactory = new GpuBufferFactory(this);
        DescriptorFactory = new DescriptorFactory(this);
        RenderPassFactory = new RenderPassFactory(this);
        CommandHandler = new CommandHandler(this);
        FrameHandler = new FrameHandler(this);
        PipelineFactory = new PipelineFactory(this);
        Debug.Log("Vulkan resources initialized.", VALIDATION_LAYERS.SUCCESS);
        
    }

    private void SetupFrameChain()
    {
        var id = ImGui.CreateContext(); //NOTE: Had missed completely that CreateContext() returns an ID.
        
        _drawData = BuildDrawData(SwapchainHandler);

        _sceneSubmissions = _drawData.Submissions ?? Array.Empty<DrawSubmission>();
        
        if (_initialGraph is not null)
        {
            // Render-graph passes own their framebuffer formats; keep base demo submissions disabled
            // until scene pipelines are authored per-pass.
            _sceneSubmissions = Array.Empty<DrawSubmission>();
        }
        
        if (_drawData.PipelineData.RenderPass.Handle == 0)
        {
            throw new InvalidOperationException("Main pipeline render pass is null before ImGui initialization.");
        }
        _imguiRenderer = new ImGuiRenderer(this);
        _imguiRenderer.Initialize(_drawData.PipelineData.RenderPass);
        
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
            //     Stride = (uint)Marshal.SizeOf<Vertex>(),
            //     Attributes =
            //     [
            //         new VertexAttribute(0, Format.R32G32B32Sfloat, 0),  // Position
            //         new VertexAttribute(1, Format.R32G32B32Sfloat, 12), // Normal
            //         new VertexAttribute(2, Format.R32G32Sfloat, 24)     // UV
            //     ]
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
            ModelMatrix = Matrix4x4.Identity,
            Submissions =
            [
                new DrawSubmission
                {
                    PassType = RenderPassType.Geometry,
                    PipelineData = pipelineData,
                    // PipelineKey = pipelineKey,
                    DescriptorSet = descriptorSet,
                    Topology = PrimitiveTopology.TriangleList,
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

    public void Dispose()
    {
        Vk.Dispose();
        VulkanDevice.Dispose();
        DescriptorFactory.Dispose();
        GpuBufferFactory.Dispose();
        PipelineFactory.Dispose();
        CommandHandler.Dispose();
    }
}