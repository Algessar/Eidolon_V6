
using System.Numerics;
using EidolonCore.Math;
using ImGuiNET;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

// INFO: UI pass = UI submission

internal sealed unsafe class ImGuiRenderer : IDisposable
{
    VulkanMaster _master;
    
    // // Lifetime (created once)
    private PipelineData _pipelineData;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool; // Not sure what this is doing here. Shouldn't this be in DescriptorFactory?
    private DescriptorSet _descriptorSet;
    // private GpuImage _fontImage; // NOTE: GpuImage is an empty struct. Should probably not be used. Use Image directly?
    private Image _fontImage;
    private ImageView _fontImageView;
    
    // Per-frame CPU state
    private DrawData _drawData; // Holds Vertex/IndexBuffers

    
    public int LastVertexCount { get; private set; }
    public int LastIndexCount { get; private set; }
    public int LastCommandListCount { get; private set; }
    
    [Header("Debug")]
    bool _showDemoWindow = true;



    public void Initialize(VulkanMaster master, RenderPass renderPass)
    {
        _master = master;
        Debug.Log("Creating ImGuiRenderer", VALIDATION_LAYERS.INFO);
        var io = ImGui.GetIO();
        if (io.Fonts.Fonts.Size == 0)
        {
            io.Fonts.AddFontDefault();
        }
        
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out var width, out var height, out var bytesPerPixel);
        if (pixels == null || width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            throw new InvalidOperationException("Failed to load font texture.");
        }
        
        io.Fonts.ClearTexData();

        CreateDescriptorResources();
        CreatePipeline(renderPass);
        
        Debug.Log("ImGuiRenderer initialized", VALIDATION_LAYERS.INFO);
    }

    private void CreateDescriptorResources()
    {
        var layoutBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &layoutBinding
        };

        if (_master.Vk.CreateDescriptorSetLayout(_master.VulkanDevice.Device, &layoutInfo, null,
                out _descriptorSetLayout) != Result.Success)
        {
            throw new Exception("Failed to create ImGui descriptor set layout.");
            
        }
        
        var poolSizes = stackalloc DescriptorPoolSize[1];
        poolSizes[0] = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = poolSizes,
            MaxSets = 1
        };
        
        if (_master.Vk.CreateDescriptorPool(_master.VulkanDevice.Device, &poolInfo, null, out _descriptorPool) != Result.Success)
        {
            throw new Exception("Failed to create ImGui descriptor pool.");
            
        }

        fixed(DescriptorSetLayout* descriptorSetLayoutPtr = &_descriptorSetLayout)
        {
            var descriptorSetAllocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = descriptorSetLayoutPtr
            };
            
            if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &descriptorSetAllocateInfo, out _descriptorSet) != Result.Success)
            {
                throw new Exception("Failed to allocate ImGui descriptor set.");
            }
        }
        
        //TODO: upload ImGui font atlas to GPU and write CombinedImageSampler descriptor at binding 0.
    }

    private void CreatePipeline(RenderPass renderPass)
    {
        var key = new PipelineKey
        {
            VertexShaderPath = "basic.vert.spv",
            FragmentShaderPath = "basic.frag.spv",
            RenderPass = renderPass,
            Layout = _descriptorSetLayout,
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
            EnableBlending = true,
            BlendState = BlendState.AlphaBlending,
        };

        _pipelineData = _master.PipelineFactory.GetOrCreate(key);
    }   

    public void NewFrame(float delta, Vector2 size)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = size;
        io.DeltaTime = Mathf.Max(1f / 1000f, delta);
        ImGui.NewFrame();
    }

    public void BuildUI()
    {
        ImGui.Begin("Eidolon / Render Graph");
        ImGui.Text("ImGui is integrated in the render graph frame lifecycle!");
        
        ImGui.Text($"CmdListst count: {LastCommandListCount}, Vtx: {LastVertexCount}, Idx: {LastIndexCount}");
        ImGui.Checkbox("Show Demo Window", ref _showDemoWindow);
        ImGui.End();
        
        if(_showDemoWindow)
        {
            ImGui.ShowDemoWindow(ref _showDemoWindow);
        }
    }
    public void FinalizeFrame()
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        LastVertexCount = !drawData.Valid ? 0 : drawData.TotalVtxCount;
        LastIndexCount = !drawData.Valid ? 0 :drawData.TotalIdxCount;
        LastCommandListCount =!drawData.Valid ? 0 : drawData.CmdListsCount;
    }
    
    public void AddToGraph(RenderGraphBuilder builder, ResourceHandle sourceColor, ResourceHandle target)
    {
        builder.AddPass("ImGui", RenderPassType.Ui)
            .Read(sourceColor)
            .Write(target);
    }

    public void Dispose()
    {
        if (_descriptorPool.Handle != 0)
        {
            _master.Vk.DestroyDescriptorPool(_master.VulkanDevice.Device, _descriptorPool, null);
            _descriptorPool = default;
        }

        if (_descriptorSetLayout.Handle != 0)
        {
            _master.Vk.DestroyDescriptorSetLayout(_master.VulkanDevice.Device, _descriptorSetLayout, null);
            _descriptorSetLayout = default;
        }
    }
}