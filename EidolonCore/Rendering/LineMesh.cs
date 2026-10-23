using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// A list of unindexed line segments (pairs of vertices), for grid and gizmo overlays.
/// </summary>
public sealed class LineMesh
{
    public LineMesh(string name, LineVertex[] vertices)
    {
        if (vertices.Length % 2 != 0)
            throw new ArgumentException("Line meshes are built from pairs of vertices.", nameof(vertices));

        Name = name;
        Vertices = vertices;
    }

    public string Name { get; }

    public LineVertex[] Vertices { get; }

    public int LineCount => Vertices.Length / 2;

    public bool IsValid => Vertices.Length >= 2;

    public override string ToString() => $"LineMesh({Name}, {LineCount} lines)";
}
