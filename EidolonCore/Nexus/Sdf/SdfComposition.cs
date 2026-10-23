using System.Numerics;

namespace EidolonCore.Nexus.Sdf;

/// <summary>
/// Boolean composition of SDFs. The result is just another <see cref="ISdf"/>, so consumers can
/// compose without knowing that a surface is built from several primitives.
/// </summary>
/// <remarks>
/// Distances stay exact for these operations; normals are taken from the branch that wins the
/// distance test, which is exact away from the seams and only ambiguous exactly on one.
/// </remarks>
public readonly record struct SdfUnion(ISdf A, ISdf B) : ISdf
{
    public float Distance(Vector3 position) => MathF.Min(A.Distance(position), B.Distance(position));

    public Vector3 Normal(Vector3 position)
    {
        var a = A.Sample(position);
        var b = B.Sample(position);
        return a.Distance <= b.Distance ? a.Normal : b.Normal;
    }
}

/// <summary>Everything both fields consider inside.</summary>
public readonly record struct SdfIntersection(ISdf A, ISdf B) : ISdf
{
    public float Distance(Vector3 position) => MathF.Max(A.Distance(position), B.Distance(position));

    public Vector3 Normal(Vector3 position)
    {
        var a = A.Sample(position);
        var b = B.Sample(position);
        return a.Distance >= b.Distance ? a.Normal : b.Normal;
    }
}

/// <summary>Everything <see cref="A"/> considers inside, minus everything <see cref="B"/> does.</summary>
public readonly record struct SdfSubtraction(ISdf A, ISdf B) : ISdf
{
    public float Distance(Vector3 position) => MathF.Max(A.Distance(position), -B.Distance(position));

    public Vector3 Normal(Vector3 position)
    {
        var a = A.Sample(position);
        var b = B.Sample(position);

        // Inside B the surface belongs to B's boundary, so its normal is flipped.
        return a.Distance >= -b.Distance ? a.Normal : -b.Normal;
    }
}
