using Eidolon.Vulkan;
using EidolonCore.Rendering;
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

/// <summary>
/// Uploads CPU-side meshes to the GPU and hands back the data needed to draw them.
/// </summary>
/// <remarks>
/// A mesh is uploaded the first time it is drawn and cached by instance, so every entity that
/// references the same <see cref="Mesh"/> shares one pair of buffers. Buffers live for the
/// lifetime of the factory: deleting an entity does not free its mesh, because another entity
/// may still be using it. Asset lifetime and unloading is a follow-up once import exists.
/// </remarks>
internal unsafe class MeshFactory : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly Dictionary<Mesh, MeshHandle> _handles = new();
    private readonly Dictionary<MeshHandle, MeshGpuData> _meshes = new();
    private readonly Dictionary<Mesh, int> _versions = new();

    private int _nextId = 1;

    public MeshFactory(VulkanMaster master)
    {
        _master = master;
    }

    /// <summary>Resolves a mesh to its GPU buffers, uploading it on first use.</summary>
    public bool TryGet(Mesh? mesh, out MeshGpuData data)
    {
        data = default;

        if (mesh is null || !mesh.IsValid)
            return false;

        if (!_handles.TryGetValue(mesh, out var handle))
            handle = Upload(mesh);

        return _meshes.TryGetValue(handle, out data);
    }

    private MeshHandle Upload(Mesh mesh)
    {
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

        var vertexBuffer = _master.GpuBufferFactory.Create(vertexKey)[0];
        var indexBuffer = _master.GpuBufferFactory.Create(indexKey)[0];

        Upload(vertexBuffer, mesh.Vertices);
        Upload(indexBuffer, mesh.Indices);

        var handle = new MeshHandle(_nextId++);
        _meshes[handle] = new MeshGpuData(vertexBuffer,
            indexBuffer, (uint)mesh.Vertices.Length, (uint)mesh.Indices.Length,
            IndexType.Uint32);
        _handles[mesh] = handle;

        return handle;
    }

    private void Refresh(Mesh mesh, MeshGpuData data)
    {
        Upload(data.VertexBuffer, mesh.Vertices);
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

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values.ToArray())
        {
            _master.GpuBufferFactory.Destroy([mesh.VertexBuffer, mesh.IndexBuffer]);
        }

        _meshes.Clear();
        _handles.Clear();
    }
}
