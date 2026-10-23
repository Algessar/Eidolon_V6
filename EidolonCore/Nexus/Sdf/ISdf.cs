using System.Numerics;

namespace EidolonCore.Nexus.Sdf;

/// <summary>
/// A signed distance field: the distance from a position to the nearest surface, negative inside.
/// </summary>
/// <remarks>
/// This is the query surface Nexus systems talk to. Consumers never need to know whether the
/// field is analytic, composed or sampled; they only need a distance and a direction to correct
/// against. Normals point away from the surface (the gradient of the distance).
/// </remarks>
public interface ISdf
{
    /// <summary>Signed distance to the surface; negative inside, zero on it.</summary>
    float Distance(Vector3 position);

    /// <summary>Unit direction pointing away from the surface at <paramref name="position"/>.</summary>
    Vector3 Normal(Vector3 position);

    /// <summary>Distance and normal from a single evaluation.</summary>
    SdfSample Sample(Vector3 position) => new(Distance(position), Normal(position));
}

/// <summary>One SDF query result.</summary>
public readonly record struct SdfSample(float Distance, Vector3 Normal)
{
    public bool IsInside => Distance < 0f;
}
