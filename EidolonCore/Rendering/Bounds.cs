using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>Axis-aligned bounds in a mesh's local space.</summary>
public readonly record struct Bounds(Vector3 Min, Vector3 Max)
{
    public Vector3 Size => Max - Min;

    public Vector3 Center => (Min + Max) * 0.5f;

    public float MaxDimension => MathF.Max(Size.X, MathF.Max(Size.Y, Size.Z));

    public static Bounds FromVertices(ReadOnlySpan<Vertex> vertices)
    {
        if (vertices.IsEmpty)
            return new Bounds(Vector3.Zero, Vector3.Zero);

        var min = vertices[0].Position;
        var max = min;

        for (var i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i].Position);
            max = Vector3.Max(max, vertices[i].Position);
        }

        return new Bounds(min, max);
    }

    /// <summary>
    /// Slab test against the box. Cheap early-out before testing individual triangles.
    /// </summary>
    /// <remarks>
    /// A ray exactly parallel to a slab and exactly on its boundary can produce NaN and report a
    /// miss; that is acceptable for picking and avoids a special case on the hot path.
    /// </remarks>
    public bool Intersects(in Ray ray)
    {
        var inverseDirection = new Vector3(
            1f / ray.Direction.X,
            1f / ray.Direction.Y,
            1f / ray.Direction.Z);

        var near = (Min - ray.Origin) * inverseDirection;
        var far = (Max - ray.Origin) * inverseDirection;

        var smallest = Vector3.Min(near, far);
        var largest = Vector3.Max(near, far);

        var enter = MathF.Max(smallest.X, MathF.Max(smallest.Y, smallest.Z));
        var exit = MathF.Min(largest.X, MathF.Min(largest.Y, largest.Z));

        return enter <= exit && exit >= 0f;
    }
}
