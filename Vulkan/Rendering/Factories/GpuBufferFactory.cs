using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan;

internal unsafe class GpuBufferFactory : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly Dictionary<GpuBufferKey, GpuBuffer[]> _cache = new();

    public GpuBufferFactory(VulkanMaster master)
    {
        _master = master;
    }

    public GpuBuffer[] GetOrCreate(GpuBufferKey key)
    {
        if (_cache.TryGetValue(key, out var existing))
            return existing;

        var buffers = new GpuBuffer[key.Count];
        for (var i = 0; i < key.Count; i++)
        {
            buffers[i] = CreateBuffer(key.Size, key.Usage, key.MemoryProperties);
        }

        _cache[key] = buffers;
        return buffers;
    }

    public void Release(GpuBufferKey key)
    {
        if (!_cache.Remove(key, out var buffers))
            return;

        foreach (var buffer in buffers)
        {
            Destroy(buffer);
        }
    }

    public void Dispose()
    {
        foreach (var group in _cache.Values)
        {
            foreach (var buffer in group)
            {
                Destroy(buffer);
            }
        }

        _cache.Clear();
    }

    private GpuBuffer CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        if (_master.Vk.CreateBuffer(_master.VulkanDevice.Device, &bufferInfo, null, out Buffer buffer) != Result.Success)
            throw new Exception("Failed to create buffer!");

        _master.Vk.GetBufferMemoryRequirements(_master.VulkanDevice.Device, buffer, out var requirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(requirements.MemoryTypeBits, memoryProperties),
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, &allocInfo, null, out DeviceMemory memory) != Result.Success)
        {
            _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer, null);
            throw new Exception("Failed to allocate buffer memory!");
        }

        if (_master.Vk.BindBufferMemory(_master.VulkanDevice.Device, buffer, memory, 0) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer, null);
            throw new Exception("Failed to bind buffer memory!");
        }

        return new GpuBuffer
        {
            Buffer = buffer,
            Memory = memory,
            Size = size,
            Usage = usage,
            MemoryFlags = memoryProperties,
            HostVisible = memoryProperties.HasFlag(MemoryPropertyFlags.HostVisibleBit),
        };
    }

    private void Destroy(GpuBuffer buffer)
    {
        if (!buffer.IsValid)
            return;

        _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer.Buffer, null);
        _master.Vk.FreeMemory(_master.VulkanDevice.Device, buffer.Memory, null);
    }
}