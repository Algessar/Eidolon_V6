using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>
/// The local transform a system wants this entity to have, published for resolution.
/// </summary>
/// <remarks>
/// Animation writes here rather than into the transform, because more than one system may want a say in
/// the final pose. A resolver turns desired state into authoritative state; until then, animation is
/// only expressing intent. This is the split Nexus depends on to let physics outrank animation without
/// the two fighting over the transform.
/// </remarks>
public struct DesiredPose : IComponent
{
    public DesiredPose(LocalPose pose)
    {
        Pose = pose;
    }

    /// <summary>Wanted local transform.</summary>
    public LocalPose Pose;
}
