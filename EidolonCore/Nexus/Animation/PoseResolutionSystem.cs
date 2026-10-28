using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace EidolonCore.Nexus.Animation;

/// <summary>Turns desired poses into the authoritative local transforms.</summary>
/// <remarks>
/// This is the resolution step Nexus describes: writers publish what they want, and one place commits
/// it. Today animation is the only writer, so the resolver applies every desired pose it finds. When
/// physics starts claiming bones, this is where priority and layering get decided.
/// </remarks>
public sealed class PoseResolutionSystem : ISystem
{
    public void Update(World world, in FrameTime time)
    {
        foreach (var entity in world.GetEntitiesWith<DesiredPose, Transform>())
        {
            var pose = world.GetComponent<DesiredPose>(entity).Pose;

            ref var transform = ref world.GetComponent<Transform>(entity);
            transform.Position = pose.Position;
            transform.Rotation = pose.Rotation;
            transform.Scale = pose.Scale;
        }
    }
}
