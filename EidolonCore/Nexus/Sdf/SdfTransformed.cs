using System.Numerics;

namespace EidolonCore.Nexus.Sdf;

/// <summary>
/// Places a field in the world with a rigid transform, so geometry that moves can still be queried in
/// world space.
/// </summary>
/// <remarks>
/// The pose is mutable on purpose: a rigid body's shape changes place every step, and a contact
/// constraint holds on to one of these for the whole session rather than rebuilding its field each
/// frame. Only rotation and translation are supported, because that is what a rigid pose is; scale
/// belongs to the geometry itself, not to the wrapper.
/// </remarks>
public sealed class SdfTransformed : ISdf
{
    private readonly ISdf _local;
    private Quaternion _inverse = Quaternion.Identity;

    public SdfTransformed(ISdf local)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
    }

    /// <summary>The field in its own space, i.e. before the pose is applied.</summary>
    public ISdf Local => _local;

    public Vector3 Position { get; private set; }

    public Quaternion Rotation { get; private set; } = Quaternion.Identity;

    public void SetPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = Quaternion.Normalize(rotation);
        _inverse = Quaternion.Inverse(Rotation);
    }

    public float Distance(Vector3 position) => _local.Distance(ToLocal(position));

    /// <summary>The local normal rotated back into world space; distances are unaffected.</summary>
    public Vector3 Normal(Vector3 position) =>
        Vector3.Transform(_local.Normal(ToLocal(position)), Rotation);

    private Vector3 ToLocal(Vector3 position) => Vector3.Transform(position - Position, _inverse);
}
