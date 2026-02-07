using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Color;
    public Vector2 UV;
    
        
    public Vertex(Vector3 position, Vector3 normal, Vector3 color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }
    
    public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Position = position;
        Normal = normal;
        UV = uv;
    }
}

// Simple mesh data for testing
public static class TestMeshes
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