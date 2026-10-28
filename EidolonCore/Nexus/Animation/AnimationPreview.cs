using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>Samples a clip without running the simulation, for scrubbing in the editor.</summary>
/// <remarks>
/// Playback only runs in play mode, so previewing an edit needs its own entry point. It publishes
/// <see cref="DesiredPose"/> exactly like <see cref="AnimationSystem"/> does; whoever is previewing is
/// expected to resolve afterwards, which keeps the desired-versus-authoritative split intact even here.
/// </remarks>
public static class AnimationPreview
{
    /// <summary>Publishes the pose at <paramref name="time"/> for every bound bone.</summary>
    /// <returns>How many bones were posed.</returns>
    public static int Apply(World world, Entity root, AnimationClip clip, float time)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clip);

        var bindings = AnimationBindings.Resolve(world, root, clip);
        var sampleTime = System.Math.Clamp(time, 0f, clip.Duration);
        var posed = 0;

        foreach (var binding in bindings)
        {
            if (!binding.Track.TrySample(sampleTime, out var pose))
                continue;

            world.SetComponent(binding.Entity, new DesiredPose(pose));
            posed++;
        }

        return posed;
    }
}
