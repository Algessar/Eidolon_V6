using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

[StructLayout(LayoutKind.Sequential)]
internal struct Vertex
{
    public Vector3 Position;
    public Vector2 Normal;
    public Vector3 Color; // This should be Vector4 for Alpha
    public Vector2 TexCoord;
    public float Distance;

    public VertexAttribute VertexAttribute;

    public Vertex(Vector3 position, Vector3 color, Vector2 texCoord, Vector2 normal)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
        Normal = normal;
        
    }
    
    // Constructor for vertex with distance
    public Vertex(Vector3 position, Vector3 color, Vector2 texCoord, Vector2 normal, float distance)
        : this(position, color, texCoord, normal)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
        Normal = normal;
        Distance = distance;
    }

    // Simple constructor for position + color only
    public Vertex(Vector3 position, Vector3 color, Vector2 normal)
        : this(position, color, Vector2.Zero, normal)
    {
        Position = position;
        Color = color;
    }

    public Vertex(Vector3 position, Vector3 color)
    {
        Position = position;
        Color = color;
    }
}

// Simple mesh data for testing
internal static class TestMeshes
{
    public static Vertex[] CubeVertices = new Vertex[]
    {
        // Front face
        new(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(0, 0)),
        new(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(1, 0)),
        new(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(1, 1)),
        new(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(0, 1)),
        
        // Back face
        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(1, 0)),
        new(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(1, 1)),
        new(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(0, 1)),
        new(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(0, 0)),
    };

    public static uint[] CubeIndices = new uint[]
    {
        // Front face
        0, 1, 2, 2, 3, 0,
        // Back face
        4, 5, 6, 6, 7, 4,
        // Top face
        3, 2, 6, 6, 5, 3,
        // Bottom face
        0, 4, 7, 7, 1, 0,
        // Right face
        1, 7, 6, 6, 2, 1,
        // Left face
        0, 3, 5, 5, 4, 0
    };
}