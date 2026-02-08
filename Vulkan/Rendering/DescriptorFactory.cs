using Silk.NET.Vulkan;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class DescriptorFactory: IDisposable
{
    VulkanMaster _master;
    
    private DescriptorPool _descriptorPool;
    
    private GpuBuffer[]  _uniformGpuBuffers = Array.Empty<GpuBuffer>() ;
    private DescriptorSetLayout _descriptorSetLayout;
    DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();

    public DescriptorFactory(VulkanMaster master)
    {
        Debug.Log("Creating DescriptorFactory", VALIDATION_LAYERS.WARNING);
        _master = master;
        
        CreateDescriptorSetLayout();
        CreateDescriptorPool();
        CreateDescriptorSets();
        
        Debug.Log("DescriptorFactory created!", VALIDATION_LAYERS.SUCCESS);
    }


    public DescriptorSet CreateDescriptorSet()
    {


        var layouts = new DescriptorSetLayout[Constants.MAX_FRAMES_IN_FLIGHT];
        Array.Fill(layouts, _descriptorSetLayout);
        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = Constants.MAX_FRAMES_IN_FLIGHT,
                PSetLayouts = layoutsPtr
            };

            _descriptorSets = new DescriptorSet[Constants.MAX_FRAMES_IN_FLIGHT];
            if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &allocInfo, _descriptorSets) != Result.Success)
                throw new Exception("Failed to allocate descriptor sets!");
        }
        return new DescriptorSet();
    }
    
    private void CreateDescriptorSetLayout()
    {
        var uboLayoutBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,                                   // Binding point 0 in shader
            DescriptorType = DescriptorType.UniformBuffer, // It's a uniform buffer
            DescriptorCount = 1,                           // One buffer at this binding
            StageFlags = ShaderStageFlags.VertexBit,       // Accessed by vertex shader
            PImmutableSamplers = null
        };

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &uboLayoutBinding
        };

        if (_master.Vk.CreateDescriptorSetLayout(_master.VulkanDevice.Device, &layoutInfo, null, out _descriptorSetLayout) != Result.Success)
            throw new Exception("Failed to create descriptor set layout!");
    }
    
    public void CreateDescriptorPool()
    {
        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[2];

        poolSizes[0] = new DescriptorPoolSize
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = Constants.MAX_FRAMES_IN_FLIGHT
        };

        poolSizes[1] = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = 4 // ImGui font + some headroom
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
            MaxSets = Constants.MAX_FRAMES_IN_FLIGHT + 4
        };

        if (_master.Vk.CreateDescriptorPool(
                _master.VulkanDevice.Device,
                &poolInfo,
                null,
                out _descriptorPool) != Result.Success)
        {
            throw new Exception("Failed to create descriptor pool!");
        }
    }
    private void CreateDescriptorSets() //NOTE: Allocates to buffers
    {
        // Create an array of layouts (same layout for all frames)
        //NOTE: Why would GpuBuffers matter here?
        _uniformGpuBuffers = _master.BufferFactory.CreateUniformGpuBuffers(Constants.MAX_FRAMES_IN_FLIGHT);
        
        if ( _uniformGpuBuffers.Any(b => !b.IsValid))
        {
            throw new Exception("Uniform buffers are not properly created!");
        }
        
        var layouts = new DescriptorSetLayout[Constants.MAX_FRAMES_IN_FLIGHT];
        Array.Fill(layouts, _descriptorSetLayout);

        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = Constants.MAX_FRAMES_IN_FLIGHT,
                PSetLayouts = layoutsPtr
            };

            _descriptorSets = new DescriptorSet[Constants.MAX_FRAMES_IN_FLIGHT];
            if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &allocInfo, _descriptorSets) != Result.Success)
                throw new Exception("Failed to allocate descriptor sets!");
        }
    }

    public void Dispose()
    {
        _uniformGpuBuffers = Array.Empty<GpuBuffer>();
    }
}