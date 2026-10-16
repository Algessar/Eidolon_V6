using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal enum GpuBufferAllocationStrategy
{
    Static,
    PerFrame,
}

internal enum GpuBufferUsageClass
{
    HostVisible,
    Staging,
    Uniform,
    Index,
    Vertex,
}

internal readonly record struct GpuBufferKey
{
    public required GpuBufferUsageClass UsageClass { get; init; }
    public required ulong Size { get; init; }
    public required BufferUsageFlags Usage { get; init; }
    public required MemoryPropertyFlags MemoryProperties { get; init; }
    public required uint Count { get; init; }
    public required GpuBufferAllocationStrategy AllocationStrategy { get; init; }
}