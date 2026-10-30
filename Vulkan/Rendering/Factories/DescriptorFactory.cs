using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;


internal readonly record struct DescriptorData
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

//TODO: This has suddenly gone unused; the only place it is called is in VulkanMaster
// for a placeholder descriptor. Great.
internal unsafe class DescriptorFactory: IDisposable
{
    private readonly VulkanMaster _master;
    
    
    private readonly Dictionary<DescriptorKey, DescriptorData> _cache = new();
    private readonly Dictionary<DescriptorKey, DescriptorSetLayoutBinding[]> _bindingsByKey = new();
    
    private readonly DescriptorKey _defaultKey;
    private readonly DescriptorKey _imguiKey;
    private readonly DescriptorKey _skinnedKey;

    private GpuBuffer[] _defaultUniformBuffers = Array.Empty<GpuBuffer>();
    private GpuBuffer[] _boneBuffers = Array.Empty<GpuBuffer>();

    public DescriptorSetLayout Layout => GetOrCreate(_defaultKey).Layout;

    /// <summary>Layout for skinned draws: the camera block plus the bone palette storage buffer.</summary>
    public DescriptorSetLayout SkinnedLayout => GetOrCreate(_skinnedKey).Layout;

    /// <summary>How many bone matrices one frame's palette can hold.</summary>
    public const int BoneMatrixCapacity = 4096;

    private const ulong BoneMatrixBytes = 64;
    
    public DescriptorFactory(VulkanMaster master)
    {
        Debug.Log("Creating DescriptorFactory", VALIDATION_LAYERS.WARNING);
        _master = master;
        
        _defaultKey = DescriptorKey.Create(
            "b0:UniformBuffer:1:Vertex",
            Constants.MAX_FRAMES_IN_FLIGHT * ViewCount,
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

        // Skinned draws need the same camera block plus one storage buffer holding every skin's bones.
        _skinnedKey = DescriptorKey.Create(
            "b0:UniformBuffer:1:Vertex|b1:StorageBuffer:1:Vertex",
            Constants.MAX_FRAMES_IN_FLIGHT * ViewCount,
            DescriptorAllocationStrategy.PerFrame,
            [DescriptorType.UniformBuffer, DescriptorType.StorageBuffer]);

        _bindingsByKey[_skinnedKey] =
        [
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
            },
            new DescriptorSetLayoutBinding
            {
                Binding = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
            }
        ];

        _ = GetOrCreate(_defaultKey);
        _ = GetOrCreate(_skinnedKey);
        
        Debug.Log("DescriptorFactory created!", VALIDATION_LAYERS.SUCCESS);
    }


    /// <summary>How many independent views (editor + game) reuse the per-frame slots.</summary>
    public const uint ViewCount = 2;

    public DescriptorSet GetDescriptorSet(uint frameIndex, uint viewIndex = 0)
    {
        return GetOrCreate(_defaultKey).GetSet(SlotIndex(frameIndex, viewIndex));
    }

    private static uint SlotIndex(uint frameIndex, uint viewIndex) => frameIndex * ViewCount + viewIndex;
    
    public GpuBuffer GetDefaultUniformBuffer(uint frameIndex, uint viewIndex = 0)
    {
        if (_defaultUniformBuffers.Length == 0)
            throw new InvalidOperationException("Default uniform buffers are not initialized.");

        var slot = SlotIndex(frameIndex, viewIndex);

        if (slot >= _defaultUniformBuffers.Length)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return _defaultUniformBuffers[slot];
    }


    public DescriptorData GetOrCreate(DescriptorKey key)
    {
        if (_cache.TryGetValue(key, out var existing))
            return existing;

        if (!_bindingsByKey.TryGetValue(key, out var bindings))
            throw new InvalidOperationException($"Descriptor bindings were not registered for key {key.LayoutBindingsSignature}.");

        var layout = CreateDescriptorSetLayout(bindings);
        var pool = CreateDescriptorPool(bindings, key.SetCount);
        var sets = AllocateDescriptorSets(layout, pool, key.SetCount);

        var bundle = new DescriptorData
        {
            Layout = layout,
            Pool = pool,
            Sets = sets,
        };

        _cache[key] = bundle;

        if (key.Equals(_defaultKey))
            CreateAndWriteDefaultUniformDescriptors(bundle);
        else if (key.Equals(_skinnedKey))
            CreateAndWriteSkinnedDescriptors(bundle);

        return bundle;
    }

    public DescriptorSet GetSkinnedDescriptorSet(uint frameIndex, uint viewIndex = 0) =>
        GetOrCreate(_skinnedKey).GetSet(SlotIndex(frameIndex, viewIndex));

    /// <summary>The frame's bone palette. Two views share it, because they draw the same world.</summary>
    public GpuBuffer GetBoneBuffer(uint frameIndex)
    {
        if (_boneBuffers.Length == 0)
            throw new InvalidOperationException("Bone palette buffers are not initialized.");

        if (frameIndex >= _boneBuffers.Length)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return _boneBuffers[frameIndex];
    }

    /// <summary>
    /// Binds the camera block and the frame's bone palette for every skinned slot. Slot order is
    /// frame-major, so both views of a frame share that frame's palette.
    /// </summary>
    private void CreateAndWriteSkinnedDescriptors(DescriptorData data)
    {
        if (_defaultUniformBuffers.Length == 0)
            throw new InvalidOperationException("The default uniform buffers must exist before skinned descriptors.");

        var key = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.HostVisible,
            Size = BoneMatrixCapacity * BoneMatrixBytes,
            Usage = BufferUsageFlags.StorageBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = Constants.MAX_FRAMES_IN_FLIGHT,
            AllocationStrategy = GpuBufferAllocationStrategy.PerFrame,
        };

        _boneBuffers = _master.GpuBufferFactory.Create(key);

        for (var slot = 0; slot < data.Sets.Length; slot++)
        {
            var cameraInfo = new DescriptorBufferInfo
            {
                Buffer = _defaultUniformBuffers[slot].Buffer,
                Offset = 0,
                Range = _defaultUniformBuffers[slot].Size,
            };

            var boneInfo = new DescriptorBufferInfo
            {
                Buffer = _boneBuffers[slot / ViewCount].Buffer,
                Offset = 0,
                Range = _boneBuffers[slot / ViewCount].Size,
            };

            var writes = stackalloc WriteDescriptorSet[2];

            writes[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = data.Sets[slot],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &cameraInfo,
            };

            writes[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = data.Sets[slot],
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                PBufferInfo = &boneInfo,
            };

            _master.Vk.UpdateDescriptorSets(_master.VulkanDevice.Device, 2, writes, 0, null);
        }
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
    
    private void CreateAndWriteDefaultUniformDescriptors(DescriptorData data)
    {
        var key = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Uniform,
            Size = (ulong)System.Runtime.InteropServices.Marshal.SizeOf<CameraUboData>(),
            Usage = BufferUsageFlags.UniformBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = Constants.MAX_FRAMES_IN_FLIGHT * ViewCount,
            AllocationStrategy = GpuBufferAllocationStrategy.PerFrame,
        };
        
        //NOTE: to self; this is *descriptor* buffer info, so it's valid here.
        // For next time I think it should be in a BufferFactory.
        var uniformBuffers = _master.GpuBufferFactory.Create(key);
        _defaultUniformBuffers = uniformBuffers;
        for (var i = 0; i < data.Sets.Length; i++)
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
                DstSet = data.Sets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfo,
            };

            _master.Vk.UpdateDescriptorSets(_master.VulkanDevice.Device, 1, &write, 0, null);
        }
    }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
        {
            if (entry.Pool.Handle != 0)
                _master.Vk.DestroyDescriptorPool(_master.VulkanDevice.Device, entry.Pool, null);

            if (entry.Layout.Handle != 0)
                _master.Vk.DestroyDescriptorSetLayout(_master.VulkanDevice.Device, entry.Layout, null);
        }

        foreach (var boneBuffer in _boneBuffers)
            _master.GpuBufferFactory.Destroy(boneBuffer);

        _boneBuffers = Array.Empty<GpuBuffer>();
        _cache.Clear();
        _bindingsByKey.Clear();
        _defaultUniformBuffers = Array.Empty<GpuBuffer>();
    }
}