using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// Built-in meshes, used to populate a scene before an asset pipeline exists.
/// </summary>
/// <remarks>
/// Each property returns the same <see cref="Mesh"/> instance every time, so entities sharing a
/// primitive share one GPU buffer.
/// </remarks>
public static class PrimitiveMeshes
{
    /// <summary>A unit cube centred on the origin, with flat per-face normals.</summary>
    public static Mesh Cube { get; } = CreateCube();

    /// <summary>A unit quad on the XZ plane, centred on the origin and facing +Y.</summary>
    public static Mesh Plane { get; } = CreatePlane();

    private static Mesh CreateCube()
    {
        // Per face: outward normal and its four corners in counter-clockwise order seen from
        // outside, so the geometric winding matches the declared normal.
        (Vector3 Normal, Vector3[] Corners)[] faces =
        [
            (new(0, 0, 1),
                [new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f)]),
            (new(0, 0, -1),
                [new(.5f, -.5f, -.5f), new(-.5f, -.5f, -.5f), new(-.5f, .5f, -.5f), new(.5f, .5f, -.5f)]),
            (new(-1, 0, 0),
                [new(-.5f, -.5f, -.5f), new(-.5f, -.5f, .5f), new(-.5f, .5f, .5f), new(-.5f, .5f, -.5f)]),
            (new(1, 0, 0),
                [new(.5f, -.5f, .5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(.5f, .5f, .5f)]),
            (new(0, 1, 0),
                [new(-.5f, .5f, .5f), new(.5f, .5f, .5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f)]),
            (new(0, -1, 0),
                [new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, -.5f, .5f), new(-.5f, -.5f, .5f)]),
        ];

        Vector2[] uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

        var vertices = new List<Vertex>(24);
        var indices = new List<uint>(36);

        foreach (var (normal, corners) in faces)
        {
            var baseIndex = (uint)vertices.Count;

            for (var i = 0; i < corners.Length; i++)
                vertices.Add(new Vertex(corners[i], normal, uvs[i]));

            indices.AddRange([baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3]);
        }

        return new Mesh("Cube", vertices.ToArray(), indices.ToArray());
    }

    private static Mesh CreatePlane()
    {
        Vertex[] vertices =
        [
            new(new Vector3(-0.5f, 0, -0.5f), Vector3.UnitY, new Vector2(0, 1)),
            new(new Vector3(0.5f, 0, -0.5f), Vector3.UnitY, new Vector2(1, 1)),
            new(new Vector3(0.5f, 0, 0.5f), Vector3.UnitY, new Vector2(1, 0)),
            new(new Vector3(-0.5f, 0, 0.5f), Vector3.UnitY, new Vector2(0, 0)),
        ];

        // Wound so the geometric normal is +Y.
        return new Mesh("Plane", vertices, [0, 2, 1, 0, 3, 2]);
    }
}
