using System.Numerics;
using Eidolon.Vulkan;

namespace Eidolon.Engine;

public class MeshFactory
{
    private static Mesh CreateTriangleMesh()  
    {  
        var vertices = new[]  
        {  
            new Vertex(new Vector3( 0.0f,  0.5f, 0.0f), new Vector3(0.0f, 0.2f, 0.0f)),  
            new Vertex(new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.0f, 0.5f, 0.0f)),  
            new Vertex(new Vector3( 0.5f, -0.5f, 0.0f), new Vector3(0.0f, 1.0f, 0.2f))  
        };  
        return new Mesh(vertices);  
    }
}