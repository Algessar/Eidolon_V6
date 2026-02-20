using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//TODO: rename to DescriptorData
internal readonly record struct DescriptorBundle
{
    public DescriptorSetLayout Layout { get; init; }
    public DescriptorPool Pool { get; init; }
    public DescriptorSet[] Sets { get; init; }

    public DescriptorSet GetSet(uint frameIndex)
    {
        if (Sets.Length == 0)
            throw new InvalidOperationException("Descriptor bundle has no descriptor sets.");

        if (frameIndex >= Sets.Length)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return Sets[frameIndex];
    }
}


internal unsafe class DescriptorFactory: IDisposable
{
    private readonly VulkanMaster _master;
    
    
    private readonly Dictionary<DescriptorKey, DescriptorBundle> _cache = new();
    private readonly Dictionary<DescriptorKey, DescriptorSetLayoutBinding[]> _bindingsByKey = new();
    
    private readonly DescriptorKey _defaultKey;
    private readonly DescriptorKey _imguiKey;
    
    private DescriptorPool _descriptorPool;
    
    private GpuBuffer[]  _uniformGpuBuffers = Array.Empty<GpuBuffer>() ;
    private DescriptorSetLayout _descriptorSetLayout;
    DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();
    
    public DescriptorSetLayout Layout => GetOrCreate(_defaultKey).Layout;
    
    public DescriptorFactory(VulkanMaster master)
    {
        Debug.Log("Creating DescriptorFactory", VALIDATION_LAYERS.WARNING);
        _master = master;
        
        _defaultKey = DescriptorKey.Create(
            "b0:UniformBuffer:1:Vertex",
            Constants.MAX_FRAMES_IN_FLIGHT,
            DescriptorAllocationStrategy.PerFrame,
            [DescriptorType.UniformBuffer]);

        _bindingsByKey[_defaultKey] =
        [
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
            }
        ];

        _imguiKey = DescriptorKey.Create(
            "b0:CombinedImageSampler:1:Fragment",
            1,
            DescriptorAllocationStrategy.Static,
            stackalloc DescriptorType[] { DescriptorType.CombinedImageSampler });

        _bindingsByKey[_imguiKey] =
        [
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
            }
        ];

        _ = GetOrCreate(_defaultKey);
        
        Debug.Log("DescriptorFactory created!", VALIDATION_LAYERS.SUCCESS);
    }
    
    public DescriptorKey GetDefaultKey() => _defaultKey;

    public DescriptorKey GetImGuiKey() => _imguiKey;

    public DescriptorSet GetDescriptorSet(uint frameIndex)
    {
        return GetOrCreate(_defaultKey).GetSet(frameIndex);
    }

    public DescriptorBundle GetOrCreate(DescriptorKey key)
    {
        if (_cache.TryGetValue(key, out var existing))
            return existing;

        if (!_bindingsByKey.TryGetValue(key, out var bindings))
            throw new InvalidOperationException($"Descriptor bindings were not registered for key {key.LayoutBindingsSignature}.");

        var layout = CreateDescriptorSetLayout(bindings);
        var pool = CreateDescriptorPool(bindings, key.SetCount);
        var sets = AllocateDescriptorSets(layout, pool, key.SetCount);

        var bundle = new DescriptorBundle
        {
            Layout = layout,
            Pool = pool,
            Sets = sets,
        };

        _cache[key] = bundle;

        if (key.Equals(_defaultKey))
        {
            CreateAndWriteDefaultUniformDescriptors(bundle);
        }

        return bundle;
    }
    
    private DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutBinding[] bindings)
    {
        fixed (DescriptorSetLayoutBinding* bindingPtr = bindings)
        {
            var layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingPtr,
            };

            if (_master.Vk.CreateDescriptorSetLayout(_master.VulkanDevice.Device, &layoutInfo, null, out var layout) != Result.Success)
                throw new Exception("Failed to create descriptor set layout!");

            return layout;
        }
    }
    
    
    
    public DescriptorSetLayout CreateDescriptorSetLayout()
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

        return _descriptorSetLayout;
    }
    
    private DescriptorPool CreateDescriptorPool(DescriptorSetLayoutBinding[] bindings, uint setCount)
    {
        var descriptorCounts = new Dictionary<DescriptorType, uint>();
        foreach (var binding in bindings)
        {
            descriptorCounts.TryGetValue(binding.DescriptorType, out var existingCount);
            descriptorCounts[binding.DescriptorType] = existingCount + (binding.DescriptorCount * setCount);
        }

        var poolSizes = descriptorCounts.Select(static kvp => new DescriptorPoolSize
        {
            Type = kvp.Key,
            DescriptorCount = kvp.Value,
        }).ToArray();

        fixed (DescriptorPoolSize* poolSizePtr = poolSizes)
        {
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizePtr,
                MaxSets = setCount,
            };

            if (_master.Vk.CreateDescriptorPool(_master.VulkanDevice.Device, &poolInfo, null, out var pool) != Result.Success)
                throw new Exception("Failed to create descriptor pool!");

            return pool;
        }
    }
    
    private DescriptorSet[] AllocateDescriptorSets(DescriptorSetLayout layout, DescriptorPool pool, uint setCount)
    {
        var layouts = new DescriptorSetLayout[setCount];
        Array.Fill(layouts, layout);

        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = pool,
                DescriptorSetCount = setCount,
                PSetLayouts = layoutsPtr,
            };

            var sets = new DescriptorSet[setCount];
            fixed (DescriptorSet* descriptorSetPtr = sets)
            {
                if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &allocInfo, descriptorSetPtr) != Result.Success)
                    throw new Exception("Failed to allocate descriptor sets!");
            }

            return sets;
        }
    }
    
    private void CreateAndWriteDefaultUniformDescriptors(DescriptorBundle bundle)
    {
        var key = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Uniform,
            Size = (ulong)sizeof(VertexAttribute),
            Usage = BufferUsageFlags.UniformBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = Constants.MAX_FRAMES_IN_FLIGHT,
            AllocationStrategy = GpuBufferAllocationStrategy.PerFrame,
        };
        
        //NOTE: to self; this is *descriptor* buffer info, so it's valid here.
        // For next time I think it should be in a BufferFactory.
        var uniformBuffers = _master.GpuBufferFactory.GetOrCreate(key);
        for (var i = 0; i < bundle.Sets.Length; i++)
        {
            var bufferInfo = new DescriptorBufferInfo
            {
                Buffer = uniformBuffers[i].Buffer,
                Offset = 0,
                Range = uniformBuffers[i].Size,
            };

            var write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = bundle.Sets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfo,
            };

            _master.Vk.UpdateDescriptorSets(_master.VulkanDevice.Device, 1, &write, 0, null);
        }
    }
    
    // public void CreateDescriptorPool()
    // {
    //     DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[2];
    //
    //     poolSizes[0] = new DescriptorPoolSize
    //     {
    //         Type = DescriptorType.UniformBuffer,
    //         DescriptorCount = Constants.MAX_FRAMES_IN_FLIGHT
    //     };
    //
    //     poolSizes[1] = new DescriptorPoolSize
    //     {
    //         Type = DescriptorType.CombinedImageSampler,
    //         DescriptorCount = 4 // ImGui font + some headroom
    //     };
    //
    //     var poolInfo = new DescriptorPoolCreateInfo
    //     {
    //         SType = StructureType.DescriptorPoolCreateInfo,
    //         PoolSizeCount = 2,
    //         PPoolSizes = poolSizes,
    //         MaxSets = Constants.MAX_FRAMES_IN_FLIGHT + 4
    //     };
    //
    //     if (_master.Vk.CreateDescriptorPool(
    //             _master.VulkanDevice.Device,
    //             &poolInfo,
    //             null,
    //             out _descriptorPool) != Result.Success)
    //     {
    //         throw new Exception("Failed to create descriptor pool!");
    //     }
    // }
    // private void CreateDescriptorSets()
    // {
    //
    //     _uniformGpuBuffers = _master.BufferFactory.CreateUniformGpuBuffers(Constants.MAX_FRAMES_IN_FLIGHT);
    //     
    //     if ( _uniformGpuBuffers.Any(b => !b.IsValid))
    //     {
    //         throw new Exception("Uniform buffers are not properly created!");
    //     }
    //     
    //     var layouts = new DescriptorSetLayout[Constants.MAX_FRAMES_IN_FLIGHT];
    //     Array.Fill(layouts, _descriptorSetLayout);
    //
    //     fixed (DescriptorSetLayout* layoutsPtr = layouts)
    //     {
    //         var allocInfo = new DescriptorSetAllocateInfo
    //         {
    //             SType = StructureType.DescriptorSetAllocateInfo,
    //             DescriptorPool = _descriptorPool,
    //             DescriptorSetCount = Constants.MAX_FRAMES_IN_FLIGHT,
    //             PSetLayouts = layoutsPtr
    //         };
    //
    //         _descriptorSets = new DescriptorSet[Constants.MAX_FRAMES_IN_FLIGHT];
    //         if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &allocInfo, _descriptorSets) != Result.Success)
    //             throw new Exception("Failed to allocate descriptor sets!");
    //         
    //         for (int i = 0; i < Constants.MAX_FRAMES_IN_FLIGHT; i++)
    //         {
    //             var bufferInfo = new DescriptorBufferInfo
    //             {
    //                 Buffer = _uniformGpuBuffers[i].Buffer,
    //                 Offset = 0,
    //                 Range = _uniformGpuBuffers[i].Size
    //             };
    //
    //             var write = new WriteDescriptorSet
    //             {
    //                 SType = StructureType.WriteDescriptorSet,
    //                 DstSet = _descriptorSets[i],
    //                 DstBinding = 0,
    //                 DstArrayElement = 0,
    //                 DescriptorType = DescriptorType.UniformBuffer,
    //                 DescriptorCount = 1,
    //                 PBufferInfo = &bufferInfo
    //             };
    //
    //             _master.Vk.UpdateDescriptorSets(
    //                 _master.VulkanDevice.Device,
    //                 1,
    //                 &write,
    //                 0,
    //                 null);
    //         }
    //     }
    //     
    // }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
        {
            if (entry.Pool.Handle != 0)
                _master.Vk.DestroyDescriptorPool(_master.VulkanDevice.Device, entry.Pool, null);

            if (entry.Layout.Handle != 0)
                _master.Vk.DestroyDescriptorSetLayout(_master.VulkanDevice.Device, entry.Layout, null);
        }

        _cache.Clear();
        _bindingsByKey.Clear();
    }
}