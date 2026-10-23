using System.Numerics;

namespace EidolonCore.Nexus.Sdf;

/// <summary>A sphere. The normal is the direction from the centre.</summary>
public readonly record struct SdfSphere(Vector3 Center, float Radius) : ISdf
{
    public float Distance(Vector3 position) => Vector3.Distance(position, Center) - Radius;

    public Vector3 Normal(Vector3 position)
    {
        var offset = position - Center;
        return offset.LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(offset);
    }
}

/// <summary>A half-space: everything below <see cref="SurfaceNormal"/> is inside.</summary>
public readonly record struct SdfPlane(Vector3 Point, Vector3 SurfaceNormal) : ISdf
{
    private Vector3 UnitNormal =>
        SurfaceNormal.LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(SurfaceNormal);

    public float Distance(Vector3 position) => Vector3.Dot(position - Point, UnitNormal);

    public Vector3 Normal(Vector3 position) => UnitNormal;
}

/// <summary>An axis-aligned box centred on the origin of its own space.</summary>
public readonly record struct SdfBox(Vector3 Center, Vector3 HalfExtents) : ISdf
{
    public float Distance(Vector3 position)
    {
        var d = LocalDelta(position);

        var outside = Vector3.Max(d, Vector3.Zero);
        var inside = MathF.Min(MathF.Max(d.X, MathF.Max(d.Y, d.Z)), 0f);

        return outside.Length() + inside;
    }

    public Vector3 Normal(Vector3 position)
    {
        var local = position - Center;
        var d = LocalDelta(position);

        if (d.X > 0f || d.Y > 0f || d.Z > 0f)
        {
            var outside = Vector3.Max(d, Vector3.Zero);
            var direction = new Vector3(
                MathF.Sign(local.X) * outside.X,
                MathF.Sign(local.Y) * outside.Y,
                MathF.Sign(local.Z) * outside.Z);

            return direction.LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(direction);
        }

        // Inside: the shallowest axis is the nearest face, so push out along it.
        if (d.X >= d.Y && d.X >= d.Z)
            return new Vector3(MathF.Sign(local.X), 0f, 0f);

        return d.Y >= d.Z
            ? new Vector3(0f, MathF.Sign(local.Y), 0f)
            : new Vector3(0f, 0f, MathF.Sign(local.Z));
    }

    private Vector3 LocalDelta(Vector3 position)
    {
        var local = Vector3.Abs(position - Center);
        return new Vector3(local.X - HalfExtents.X, local.Y - HalfExtents.Y, local.Z - HalfExtents.Z);
    }
}

/// <summary>A capsule: all points within <see cref="Radius"/> of the segment A-B.</summary>
public readonly record struct SdfCapsule(Vector3 A, Vector3 B, float Radius) : ISdf
{
    public float Distance(Vector3 position) => Vector3.Distance(position, ClosestPoint(position)) - Radius;

    public Vector3 Normal(Vector3 position)
    {
        var offset = position - ClosestPoint(position);

        if (offset.LengthSquared() >= 1e-12f)
            return Vector3.Normalize(offset);

        // On the axis itself the gradient is undefined; use the segment's perpendicular.
        var axis = B - A;
        var perpendicular = axis.LengthSquared() < 1e-12f
            ? Vector3.UnitY
            : Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.X) < 0.9f ? Vector3.UnitX : Vector3.UnitZ));

        return perpendicular.LengthSquared() < 1e-12f ? Vector3.UnitY : perpendicular;
    }

    private Vector3 ClosestPoint(Vector3 position)
    {
        var axis = B - A;
        var lengthSquared = axis.LengthSquared();

        if (lengthSquared < 1e-12f)
            return A;

        var t = System.Math.Clamp(Vector3.Dot(position - A, axis) / lengthSquared, 0f, 1f);
        return A + axis * t;
    }
}
