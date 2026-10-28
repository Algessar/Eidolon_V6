using System.Numerics;
using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>Local position, rotation and scale: the shape of one bone at one instant.</summary>
public readonly record struct LocalPose(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public static LocalPose Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    /// <summary>Reads a transform's local channels.</summary>
    public static LocalPose From(in Transform transform) =>
        new(transform.Position, transform.Rotation, transform.Scale);

    /// <summary>
    /// Blends two poses. Rotation uses the shortest arc, which is what makes a bone turn the way a
    /// person expects instead of unwinding through 270 degrees.
    /// </summary>
    public static LocalPose Interpolate(in LocalPose from, in LocalPose to, float t) =>
        new(Vector3.Lerp(from.Position, to.Position, t),
            Quaternion.Slerp(from.Rotation, to.Rotation, t),
            Vector3.Lerp(from.Scale, to.Scale, t));
}

/// <summary>One bone's pose at one time.</summary>
public readonly record struct PoseKey(float Time, LocalPose Pose);

/// <summary>Keyframes for one bone, addressed by hierarchy path.</summary>
public sealed class AnimationTrack
{
    private readonly PoseKey[] _keys;

    public AnimationTrack(string path, IEnumerable<PoseKey> keys)
    {
        ArgumentNullException.ThrowIfNull(path);

        Path = path;
        _keys = keys.OrderBy(key => key.Time).ToArray();

        if (_keys.Length == 0)
            throw new ArgumentException($"Track '{path}' has no keys.", nameof(keys));
    }

    /// <summary>
    /// Slash-separated <see cref="Name"/> path, relative to the player's entity. An empty path addresses
    /// the player's own entity, and <c>"Rotor/Blade"</c> a descendant by name.
    /// </summary>
    public string Path { get; }

    public IReadOnlyList<PoseKey> Keys => _keys;

    /// <summary>
    /// Samples the pose at <paramref name="time"/>, before and after the key range the ends hold their
    /// value rather than extrapolating.
    /// </summary>
    public bool TrySample(float time, out LocalPose pose)
    {
        pose = LocalPose.Identity;

        if (_keys.Length == 0)
            return false;

        if (time <= _keys[0].Time || _keys.Length == 1)
        {
            pose = _keys[0].Pose;
            return true;
        }

        if (time >= _keys[^1].Time)
        {
            pose = _keys[^1].Pose;
            return true;
        }

        // Clips carry a handful of keys, so a scan is cheaper than a binary search and simpler to read.
        for (var index = 1; index < _keys.Length; index++)
        {
            var to = _keys[index];

            if (to.Time < time)
                continue;

            var from = _keys[index - 1];
            var span = to.Time - from.Time;
            var blend = span <= 1e-6f ? 0f : (time - from.Time) / span;

            pose = LocalPose.Interpolate(from.Pose, to.Pose, blend);
            return true;
        }

        pose = _keys[^1].Pose;
        return true;
    }
}

/// <summary>Authored animation: tracks of keyframes over a fixed duration.</summary>
/// <remarks>
/// Plain data with no engine dependencies, so an importer (glTF, FBX) can build one directly. Playback
/// state such as looping and speed lives on <see cref="AnimationPlayer"/>, not here.
/// </remarks>
public sealed class AnimationClip
{
    public AnimationClip(string name, float duration, IEnumerable<AnimationTrack> tracks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (duration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(duration), "A clip needs a positive duration.");

        Name = name;
        Duration = duration;
        Tracks = tracks.OrderBy(track => track.Path, StringComparer.Ordinal).ToArray();
    }

    public string Name { get; }

    /// <summary>Length of one pass, in seconds.</summary>
    public float Duration { get; }

    public IReadOnlyList<AnimationTrack> Tracks { get; }

    public override string ToString() => $"AnimationClip({Name}, {Duration:0.##}s, {Tracks.Count} tracks)";
}
