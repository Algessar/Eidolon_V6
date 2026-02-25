using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

public sealed class Mesh
{
    public ReadOnlyMemory<byte> VertexData;
    public ReadOnlyMemory<byte> IndexData;

    public uint VertexCount;
    public uint IndexCount;

    public PrimitiveTopology Topology;
}