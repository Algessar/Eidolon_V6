namespace Eidolon.Vulkan;

internal class Mesh
{
    public Vertex[] Vertices { get; set; }
    public uint[] Indices { get; set; }

    public bool IsValid => Vertices.Length > 0 && Indices.Length > 0;

    public Mesh(Vertex[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}
