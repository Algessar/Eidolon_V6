using ImGuiNET;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//TODO: This is a very shitty name ^^ 
internal sealed unsafe class UiGeometryUploader : IDisposable
{
    private readonly VulkanMaster _master;
    private GpuBuffer[] _uiVertexBuffers = Array.Empty<GpuBuffer>();
    private GpuBuffer[] _uiIndexBuffers = Array.Empty<GpuBuffer>();

    public UiGeometryUploader(VulkanMaster master)
    {
        _master = master;
    }

    public ref GpuBuffer GetCurrentFrameVertexBuffer(uint currentFrame)
    {
        return ref _uiVertexBuffers[(int)currentFrame];
    }

    public ref GpuBuffer GetCurrentFrameIndexBuffer(uint currentFrame)
    {
        return ref _uiIndexBuffers[(int)currentFrame];
    }

    public void EnsureUiFrameArrays(uint currentFrame, uint maxFramesInFlight)
    {
        var frameCount = (int)maxFramesInFlight;
        if (frameCount <= 0)
            throw new InvalidOperationException("MAX_FRAMES_IN_FLIGHT must be greater than zero.");

        if (_uiVertexBuffers.Length != frameCount)
            _uiVertexBuffers = new GpuBuffer[frameCount];

        if (_uiIndexBuffers.Length != frameCount)
            _uiIndexBuffers = new GpuBuffer[frameCount];

        if (currentFrame >= frameCount)
            throw new ArgumentOutOfRangeException(nameof(currentFrame));
    }

    public void EnsureCurrentFrameUiBuffers(ImGuiDrawData drawData, uint currentFrame, uint maxFramesInFlight)
    {
        EnsureUiFrameArrays(currentFrame, maxFramesInFlight);

        ref var vertexBuffer = ref _uiVertexBuffers[(int)currentFrame];
        ref var indexBuffer = ref _uiIndexBuffers[(int)currentFrame];

        var vertexBytes = (ulong)(drawData.Vertices.Length * sizeof(ImGuiVertex));
        var indexBytes = (ulong)(drawData.Indices.Length * sizeof(ushort));

        if (!vertexBuffer.IsValid || vertexBuffer.Size < vertexBytes)
        {
            DestroyBuffer(ref vertexBuffer);
            vertexBuffer = CreateHostVisibleBuffer(vertexBytes, BufferUsageFlags.VertexBufferBit);
        }

        if (!indexBuffer.IsValid || indexBuffer.Size < indexBytes)
        {
            DestroyBuffer(ref indexBuffer);
            indexBuffer = CreateHostVisibleBuffer(indexBytes, BufferUsageFlags.IndexBufferBit);
        }
    }

    public void UploadCurrentFrameUiData(ImGuiDrawData drawData, uint currentFrame, uint maxFramesInFlight)
    {
        EnsureUiFrameArrays(currentFrame, maxFramesInFlight);

        ref var vertexBuffer = ref _uiVertexBuffers[(int)currentFrame];
        ref var indexBuffer = ref _uiIndexBuffers[(int)currentFrame];

        if (!vertexBuffer.IsValid || !indexBuffer.IsValid)
            return;

        void* mapped;
        var vertexBytes = (nuint)(drawData.Vertices.Length * sizeof(ImGuiVertex));
        fixed (ImGuiVertex* srcVertices = drawData.Vertices)
        {
            _master.Vk.MapMemory(_master.VulkanDevice.Device, vertexBuffer.Memory, 0, vertexBuffer.Size, 0, &mapped);
            global::System.Buffer.MemoryCopy(srcVertices, mapped, vertexBuffer.Size, vertexBytes);
            _master.Vk.UnmapMemory(_master.VulkanDevice.Device, vertexBuffer.Memory);
        }

        var indexBytes = (nuint)(drawData.Indices.Length * sizeof(ushort));
        fixed (ushort* srcIndices = drawData.Indices)
        {
            _master.Vk.MapMemory(_master.VulkanDevice.Device, indexBuffer.Memory, 0, indexBuffer.Size, 0, &mapped);
            global::System.Buffer.MemoryCopy(srcIndices, mapped, indexBuffer.Size, indexBytes);
            _master.Vk.UnmapMemory(_master.VulkanDevice.Device, indexBuffer.Memory);
        }
    }

    private GpuBuffer CreateHostVisibleBuffer(ulong size, BufferUsageFlags usage)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        if (_master.Vk.CreateBuffer(_master.VulkanDevice.Device, in bufferInfo, null, out var buffer) != Result.Success)
            throw new Exception("Failed to create UI buffer.");

        _master.Vk.GetBufferMemoryRequirements(_master.VulkanDevice.Device, buffer, out var requirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit),
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, in allocInfo, null, out var memory) != Result.Success)
        {
            _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer, null);
            throw new Exception("Failed to allocate UI buffer memory.");
        }

        if (_master.Vk.BindBufferMemory(_master.VulkanDevice.Device, buffer, memory, 0) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer, null);
            throw new Exception("Failed to bind UI buffer memory.");
        }

        return new GpuBuffer
        {
            Buffer = buffer,
            Memory = memory,
            Size = size,
            Usage = usage,
            MemoryFlags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            HostVisible = true,
        };
    }

    private void DestroyBuffer(ref GpuBuffer buffer)
    {
        if (!buffer.IsValid)
            return;

        _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, buffer.Buffer, null);
        _master.Vk.FreeMemory(_master.VulkanDevice.Device, buffer.Memory, null);
        buffer = default;
    }

    public void Dispose()
    {
        for (var i = 0; i < _uiVertexBuffers.Length; i++)
        {
            DestroyBuffer(ref _uiVertexBuffers[i]);
            DestroyBuffer(ref _uiIndexBuffers[i]);
        }
    }
}