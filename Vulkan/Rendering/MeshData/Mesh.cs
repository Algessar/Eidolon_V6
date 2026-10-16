using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;
internal class Mesh
{
    internal Geometry BackendGeometry { get; set; }
    public Vertex[] Vertices { get; set; }
    public uint[] Indices { get; set; }

    public bool IsValid => Vertices.Length > 0 && Indices.Length > 0;

    public Mesh(Vertex[] vertices, uint[] indices = null)
    {
        Vertices = vertices;
        Indices = indices ?? GenerateIndices(vertices.Length); // what is this bullshit though?
    }

    private static uint[] GenerateIndices(int vertexCount)
    {
        // Simple triangle list indices
        var indices = new uint[vertexCount];
        for (uint i = 0; i < vertexCount; i++)
            indices[i] = i;
        return indices;
    }
}

public enum Geometry
{
    
}