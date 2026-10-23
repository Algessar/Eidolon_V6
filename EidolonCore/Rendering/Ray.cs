using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>A ray in world space.</summary>
/// <param name="Origin">Start point.</param>
/// <param name="Direction">Unit direction.</param>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    public Vector3 PointAt(float distance) => Origin + Direction * distance;
}
