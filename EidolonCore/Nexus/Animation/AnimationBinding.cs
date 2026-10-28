using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>Turns a clip's paths into entities, relative to the entity that owns the player.</summary>
/// <remarks>
/// Shared by playback and by editor preview, so a path means the same thing whether the simulation is
/// running or not.
/// </remarks>
public readonly record struct AnimationBinding(AnimationTrack Track, Entity Entity);

public static class AnimationBindings
{
    /// <summary>Resolves every track the hierarchy answers for, skipping paths with no match.</summary>
    public static List<AnimationBinding> Resolve(World world, Entity root, AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var bindings = new List<AnimationBinding>(clip.Tracks.Count);

        foreach (var track in clip.Tracks)
        {
            if (Hierarchy.TryFindByPath(world, root, track.Path, out var bone))
                bindings.Add(new AnimationBinding(track, bone));
        }

        return bindings;
    }
}
