using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>Edits clips: adding tracks and keying poses.</summary>
/// <remarks>
/// Clips are immutable data, so every edit returns a new clip. That keeps a clip that another player is
/// already playing from changing under it, and makes undo a matter of keeping the old reference.
/// </remarks>
public static class AnimationEditing
{
    /// <summary>Keys closer together than this count as the same key.</summary>
    private const float KeyEpsilon = 1e-3f;

    /// <summary>
    /// Returns a clip with one key added or replaced on the named track, creating the track when the clip
    /// does not have it yet.
    /// </summary>
    /// <param name="time">Key time in seconds, clamped into the clip.</param>
    public static AnimationClip Key(AnimationClip clip, string path, float time, in LocalPose pose)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(path);

        var keyTime = System.Math.Clamp(time, 0f, clip.Duration);
        var tracks = new List<AnimationTrack>(clip.Tracks.Count);
        var replaced = false;

        foreach (var track in clip.Tracks)
        {
            if (!string.Equals(track.Path, path, StringComparison.Ordinal))
            {
                tracks.Add(track);
                continue;
            }

            var keys = track.Keys.ToList();

            // Keying exactly on an existing key overwrites it rather than stacking a duplicate.
            var existing = keys.FindIndex(key => MathF.Abs(key.Time - keyTime) <= KeyEpsilon);

            if (existing >= 0)
                keys[existing] = new PoseKey(keyTime, pose);
            else
                keys.Add(new PoseKey(keyTime, pose));

            tracks.Add(new AnimationTrack(track.Path, keys));
            replaced = true;
        }

        if (!replaced)
            tracks.Add(new AnimationTrack(path, [new PoseKey(keyTime, pose)]));

        return new AnimationClip(clip.Name, clip.Duration, tracks);
    }

    /// <summary>Returns a clip with one key removed. Missing tracks and keys are ignored.</summary>
    public static AnimationClip RemoveKey(AnimationClip clip, string path, float time)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var tracks = new List<AnimationTrack>(clip.Tracks.Count);

        foreach (var track in clip.Tracks)
        {
            if (!string.Equals(track.Path, path, StringComparison.Ordinal))
            {
                tracks.Add(track);
                continue;
            }

            var keys = track.Keys
                .Where(key => MathF.Abs(key.Time - time) > KeyEpsilon)
                .ToList();

            // A track with no keys left is meaningless, so the track goes with its last key.
            if (keys.Count > 0)
                tracks.Add(new AnimationTrack(track.Path, keys));
        }

        return new AnimationClip(clip.Name, clip.Duration, tracks);
    }
}
