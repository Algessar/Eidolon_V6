using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan;

/// <summary>
/// Allocates device buffers. Ownership transfers to the caller: every buffer returned by
/// <see cref="Create"/> must be handed back through <see cref="Destroy"/> (or left to
/// <see cref="Dispose"/> at shutdown).
/// </summary>
/// <remarks>
/// Buffers are deliberately NOT pooled or shared. They are host-visible and get written to
/// directly (mesh uploads, per-frame UI geometry), so two allocations with identical
/// characteristics must never alias the same VkBuffer.
/// </remarks>
internal unsafe class GpuBufferFactory : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly Dictionary<ulong, GpuBuffer> _liveBuffers = new();

    public GpuBufferFactory(VulkanMaster master)
    {
        _master = master;
    }

    public GpuBuffer[] Create(GpuBufferKey key)
    {
        var buffers = new GpuBuffer[key.Count];
        for (var i = 0; i < key.Count; i++)
        {
            buffers[i] = CreateBuffer(key.Size, key.Usage, key.MemoryProperties);
            _liveBuffers[buffers[i].Buffer.Handle] = buffers[i];
        }

        return buffers;
    }

    public void Destroy(GpuBuffer buffer)
    {
        if (!_liveBuffers.Remove(buffer.Buffer.Handle, out var tracked))
            return;

        //TODO: temp fix, find a better solution to avoid CPU-GPU sync stall
        _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);

        DestroyBuffer(tracked);
    }

    public void Destroy(IEnumerable<GpuBuffer> buffers)
    {
        foreach (var buffer in buffers)
            Destroy(buffer);
    }

    public void Dispose()
    {
        foreach (var buffer in _liveBuffers.Values)
            DestroyBuffer(buffer);

        _liveBuffers.Clear();
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

    private void DestroyBuffer(GpuBuffer buffer)
    {
        if (!buffer.IsValid)
            return;

        _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer.Buffer, null);
        _master.Vk.FreeMemory(_master.VulkanDevice.Device, buffer.Memory, null);
    }
}
