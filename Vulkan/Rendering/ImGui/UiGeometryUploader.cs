using ImGuiNET;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//TODO: This is a very shitty name ^^ 
internal sealed unsafe class UiGeometryUploader : IDisposable
{
    private readonly VulkanMaster _master;
    private GpuBuffer[] _uiVertexBuffers = Array.Empty<GpuBuffer>();
    private GpuBuffer[] _uiIndexBuffers = Array.Empty<GpuBuffer>();
    
    private GpuBufferKey? _vertexKey;
    private GpuBufferKey? _indexKey;
    
    public ref GpuBuffer GetCurrentFrameVertexBuffer(uint currentFrame) => ref _uiVertexBuffers[(int)currentFrame];

    public UiGeometryUploader(VulkanMaster master)
    {
        _master = master;
    }

    public ref GpuBuffer GetCurrentFrameIndexBuffer(uint currentFrame) => ref _uiIndexBuffers[(int)currentFrame];

    public void EnsureCurrentFrameUiBuffers(ImGuiDrawData drawData, uint currentFrame, uint maxFramesInFlight)
    {
        var frameCount = (int)maxFramesInFlight;
        if (frameCount <= 0)
            throw new InvalidOperationException("MAX_FRAMES_IN_FLIGHT must be greater than zero.");
        
        Debug.Log($"Ensuring {frameCount} ImGui frame buffers are allocated.", VALIDATION_LAYERS.INFO);

        if (currentFrame >= maxFramesInFlight)
            throw new ArgumentOutOfRangeException(nameof(currentFrame));

        var vertexBytes = Math.Max((ulong)(drawData.Vertices.Length * sizeof(ImGuiVertex)), 1UL);
        var indexBytes = Math.Max((ulong)(drawData.Indices.Length * sizeof(ushort)), 1UL);

        EnsureVertexBuffers(vertexBytes, maxFramesInFlight);
        EnsureIndexBuffers(indexBytes, maxFramesInFlight);
    }


    public void UploadCurrentFrameUiData(ImGuiDrawData drawData, uint currentFrame, uint maxFramesInFlight)
    {
        if (_uiVertexBuffers.Length != (int)maxFramesInFlight || _uiIndexBuffers.Length != (int)maxFramesInFlight)
            return;

        ref var vertexBuffer = ref _uiVertexBuffers[(int)currentFrame];
        ref var indexBuffer = ref _uiIndexBuffers[(int)currentFrame];

        if (!vertexBuffer.IsValid || !indexBuffer.IsValid)
            return;

        void* mapped;
        var vertexBytes = (nuint)(drawData.Vertices.Length * sizeof(ImGuiVertex));
        if (vertexBytes > 0)
        {
            fixed (ImGuiVertex* srcVertices = drawData.Vertices)
            {
                _master.Vk.MapMemory(_master.VulkanDevice.Device, vertexBuffer.Memory, 0, vertexBytes, 0, &mapped);
                global::System.Buffer.MemoryCopy(srcVertices, mapped, vertexBytes, vertexBytes);
                _master.Vk.UnmapMemory(_master.VulkanDevice.Device, vertexBuffer.Memory);
            }
        }

        var indexBytes = (nuint)(drawData.Indices.Length * sizeof(ushort));
        if (indexBytes > 0)
        {
            fixed (ushort* srcIndices = drawData.Indices)
            {
                _master.Vk.MapMemory(_master.VulkanDevice.Device, indexBuffer.Memory, 0, indexBytes, 0, &mapped);
                global::System.Buffer.MemoryCopy(srcIndices, mapped, indexBytes, indexBytes);
                _master.Vk.UnmapMemory(_master.VulkanDevice.Device, indexBuffer.Memory);
            }
        }
    }
    private void EnsureVertexBuffers(ulong vertexBytes, uint maxFramesInFlight)
    {
        if (_vertexKey is { } key && key.Size >= vertexBytes)
            return;

        if (_vertexKey is { } oldKey)
        {
            _master.GpuBufferFactory.Release(oldKey);
        }

        var newKey = new GpuBufferKey
        {
            Size = vertexBytes,
            Usage = BufferUsageFlags.VertexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = maxFramesInFlight,
            UsageClass = GpuBufferUsageClass.Vertex,
            AllocationStrategy = GpuBufferAllocationStrategy.PerFrame
        };

        _uiVertexBuffers = _master.GpuBufferFactory.GetOrCreate(newKey);
        _vertexKey = newKey;
    }
    
    private void EnsureIndexBuffers(ulong indexBytes, uint maxFramesInFlight)
    {
        if (_indexKey is { } key && key.Size >= indexBytes)
            return;

        if (_indexKey is { } oldKey)
            _master.GpuBufferFactory.Release(oldKey);

        var newKey = new GpuBufferKey
        {
            Size = indexBytes,
            Usage = BufferUsageFlags.IndexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = maxFramesInFlight,
            UsageClass = GpuBufferUsageClass.Index,
            AllocationStrategy = GpuBufferAllocationStrategy.PerFrame
        };

        _uiIndexBuffers = _master.GpuBufferFactory.GetOrCreate(newKey);
        _indexKey = newKey;
    }

    public void Dispose()
    {
        if (_vertexKey is { } vertexKey)
        {
            _master.GpuBufferFactory.Release(vertexKey);
            _vertexKey = null;
        }

        if (_indexKey is { } indexKey)
        {
            _master.GpuBufferFactory.Release(indexKey);
            _indexKey = null;
        }

        _uiVertexBuffers = Array.Empty<GpuBuffer>();
        _uiIndexBuffers = Array.Empty<GpuBuffer>();
    }
}