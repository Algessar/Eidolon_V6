using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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

// Simple mesh data for testing.
// The cube uses 24 vertices (4 per face) so every face can carry a flat colour that
// makes the 3D shape readable without any lighting in the shader.
internal static class TestMeshes
{
    public static Vertex[] CubeVertices = new Vertex[]
    {
        // Front face (+Z)
        new(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0.90f, 0.30f, 0.30f)),
        new(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0.90f, 0.30f, 0.30f)),
        new(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0.90f, 0.30f, 0.30f)),
        new(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0.90f, 0.30f, 0.30f)),

        // Back face (-Z)
        new(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0.30f, 0.75f, 0.35f)),
        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.30f, 0.75f, 0.35f)),
        new(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0.30f, 0.75f, 0.35f)),
        new(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0.30f, 0.75f, 0.35f)),

        // Left face (-X)
        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.30f, 0.45f, 0.95f)),
        new(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0.30f, 0.45f, 0.95f)),
        new(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0.30f, 0.45f, 0.95f)),
        new(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0.30f, 0.45f, 0.95f)),

        // Right face (+X)
        new(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0.95f, 0.85f, 0.30f)),
        new(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0.95f, 0.85f, 0.30f)),
        new(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0.95f, 0.85f, 0.30f)),
        new(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0.95f, 0.85f, 0.30f)),

        // Top face (+Y)
        new(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0.35f, 0.90f, 0.90f)),
        new(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0.35f, 0.90f, 0.90f)),
        new(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0.35f, 0.90f, 0.90f)),
        new(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0.35f, 0.90f, 0.90f)),

        // Bottom face (-Y)
        new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.85f, 0.35f, 0.85f)),
        new(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0.85f, 0.35f, 0.85f)),
        new(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0.85f, 0.35f, 0.85f)),
        new(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0.85f, 0.35f, 0.85f)),
    };

    public static uint[] CubeIndices = new uint[]
    {
        0, 1, 2, 0, 2, 3,       // Front
        4, 5, 6, 4, 6, 7,       // Back
        8, 9, 10, 8, 10, 11,    // Left
        12, 13, 14, 12, 14, 15, // Right
        16, 17, 18, 16, 18, 19, // Top
        20, 21, 22, 20, 22, 23, // Bottom
    };
}