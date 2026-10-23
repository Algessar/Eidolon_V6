using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// CPU-side mesh data: an indexed triangle list. Meshes are immutable; the renderer uploads them
/// to the GPU on first use, so a mesh can exist before any device does.
/// </summary>
public sealed class Mesh
{
    private const float ParallelEpsilon = 1e-7f;

    public string Name { get; }
    public Vertex[] Vertices { get; }
    public uint[] Indices { get; }

    /// <summary>Axis-aligned bounds in local space, computed once at construction.</summary>
    public Bounds LocalBounds { get; }

    public bool IsValid => Vertices.Length > 0 && Indices.Length > 0;

    public int TriangleCount => Indices.Length / 3;

    public Mesh(string name, Vertex[] vertices, uint[] indices)
    {
        Name = name;
        Vertices = vertices;
        Indices = indices;
        LocalBounds = Bounds.FromVertices(vertices);
    }

    public Mesh(Vertex[] vertices, uint[] indices)
        : this("Mesh", vertices, indices)
    {
    }

    /// <summary>
    /// Tests a world-space ray against the triangles, returning the distance along the ray in
    /// world units. The ray is moved into local space so the vertices do not have to be
    /// transformed.
    /// </summary>
    public bool TryIntersect(in Ray worldRay, Matrix4x4 worldMatrix, out float distance)
    {
        distance = 0f;

        if (!Matrix4x4.Invert(worldMatrix, out var inverse))
            return false;

        var localOrigin = Vector3.Transform(worldRay.Origin, inverse);
        var localDirection = Vector3.Transform(worldRay.Origin + worldRay.Direction, inverse) - localOrigin;

        // How many local units one world unit covers; used to convert the hit back to world space.
        var scale = localDirection.Length();
        if (scale < 1e-8f)
            return false;

        localDirection /= scale;

        var localRay = new Ray(localOrigin, localDirection);
        if (!LocalBounds.Intersects(localRay))
            return false;

        var closest = float.MaxValue;

        for (var i = 0; i + 2 < Indices.Length; i += 3)
        {
            var a = Vertices[Indices[i]].Position;
            var b = Vertices[Indices[i + 1]].Position;
            var c = Vertices[Indices[i + 2]].Position;

            if (IntersectsTriangle(localRay, a, b, c, out var hit) && hit < closest)
                closest = hit;
        }

        if (closest == float.MaxValue)
            return false;

        distance = closest / scale;
        return true;
    }

    /// <summary>Möller–Trumbore, two-sided so back faces are pickable too.</summary>
    private static bool IntersectsTriangle(in Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;

        var edge1 = b - a;
        var edge2 = c - a;

        var h = Vector3.Cross(ray.Direction, edge2);
        var determinant = Vector3.Dot(edge1, h);

        if (MathF.Abs(determinant) < ParallelEpsilon)
            return false;

        var inverseDeterminant = 1f / determinant;
        var s = ray.Origin - a;

        var u = Vector3.Dot(s, h) * inverseDeterminant;
        if (u < 0f || u > 1f)
            return false;

        var q = Vector3.Cross(s, edge1);
        var v = Vector3.Dot(ray.Direction, q) * inverseDeterminant;
        if (v < 0f || u + v > 1f)
            return false;

        var t = Vector3.Dot(edge2, q) * inverseDeterminant;
        if (t <= ParallelEpsilon)
            return false;

        distance = t;
        return true;
    }

    public override string ToString() => $"Mesh({Name}, {Vertices.Length} verts, {TriangleCount} tris)";
}
