using System.Numerics;
using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace Eidolon.Engine;

internal readonly record struct MeshHandle(int ID)
{
    public bool IsValid => ID > 0;
}

internal readonly record struct MeshGpuData(
    GpuBuffer VertexBuffer,
    GpuBuffer IndexBuffer,
    uint VertexCount,
    uint IndexCount,
    IndexType IndexType);

internal unsafe class MeshFactory
{
    private readonly VulkanMaster _master;
    private readonly Dictionary<MeshHandle, MeshGpuData> _meshes = new();
    private readonly Dictionary<MeshHandle, (GpuBufferKey VertexKey, GpuBufferKey IndexKey)> _bufferKeys = new();

    private int _nextId = 1;

    public MeshFactory(VulkanMaster master)
    {
        _master = master;
    }

    public MeshHandle CreateStatic(Mesh mesh)
    {
        if (mesh is null)
            throw new ArgumentNullException(nameof(mesh));

        if (mesh.Vertices is null || mesh.Vertices.Length == 0)
            throw new InvalidOperationException("Mesh must contain at least one vertex.");

        if (mesh.Indices is null || mesh.Indices.Length == 0)
            throw new InvalidOperationException("Mesh must contain at least one index.");

        var vertexBytes = checked((ulong)(mesh.Vertices.Length * sizeof(Vertex)));
        var indexBytes = checked((ulong)(mesh.Indices.Length * sizeof(uint)));

        var vertexKey = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Vertex,
            Size = vertexBytes,
            Usage = BufferUsageFlags.VertexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = 1,
            AllocationStrategy = GpuBufferAllocationStrategy.Static

        };
        
        var indexKey = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Index,
            Size = indexBytes,
            Usage = BufferUsageFlags.IndexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = 1,
            AllocationStrategy = GpuBufferAllocationStrategy.Static,
        };

        var vertexBuffer = _master.GpuBufferFactory.GetOrCreate(vertexKey)[0];
        var indexBuffer = _master.GpuBufferFactory.GetOrCreate(indexKey)[0];

        Upload(vertexBuffer, mesh.Vertices);
        Upload(indexBuffer, mesh.Indices);

        var handle = new MeshHandle(_nextId++);
        _meshes[handle] = new MeshGpuData(vertexBuffer,
            indexBuffer, (uint)mesh.Vertices.Length, (uint)mesh.Indices.Length,
            IndexType.Uint32);

        return handle;
    }

    public bool TryGet(MeshHandle handle, out MeshGpuData mesh)
    {
        return _meshes.TryGetValue(handle, out mesh);
    }

    private void Upload<T>(GpuBuffer buffer, T[] source) where T : unmanaged
    {
        if (!buffer.IsValid)
            throw new InvalidOperationException("Cannot upload into an invalid GPU buffer.");

        if (!buffer.HostVisible)
            throw new InvalidOperationException("Cannot upload into a non host-visible GPU buffer.");

        var uploadSize = checked((ulong)(source.Length * sizeof(T)));
        if (uploadSize == 0)
            return;

        void* mapped = null;
        var mapResult = _master.Vk.MapMemory(_master.VulkanDevice.Device, buffer.Memory, 0, uploadSize, 0, &mapped);
        if (mapResult != Result.Success || mapped is null)
            throw new InvalidOperationException($"Failed to map GPU buffer memory. Result: {mapResult}");

        fixed (T* src = source)
        {
            global::System.Buffer.MemoryCopy(src, mapped, buffer.Size, uploadSize);
        }

        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, buffer.Memory);
    }
    
    
    public void Destroy(MeshHandle handle)
    {
        if (_bufferKeys.Remove(handle, out var keys)) return;

        _master.GpuBufferFactory.Release(keys.VertexKey);
        _master.GpuBufferFactory.Release(keys.IndexKey);
        _meshes.Remove(handle);
    }
    
    public void Dispose()
    {
        foreach (var (_, keys) in _bufferKeys)
        {
            _master.GpuBufferFactory.Release(keys.VertexKey);
            _master.GpuBufferFactory.Release(keys.IndexKey);
        }

        _bufferKeys.Clear();
        _meshes.Clear();
    }
}