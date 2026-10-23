using System.Numerics;
using System.Runtime.InteropServices;

namespace EidolonCore.Rendering;

/// <summary>
/// Interleaved vertex layout shared by every mesh in the engine: position, normal and texture
/// coordinate. Colour is not part of the vertex; it comes from the entity's material.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;

    public Vertex(Vector3 position, Vector3 normal, Vector2 texCoord = default)
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
    }
}
