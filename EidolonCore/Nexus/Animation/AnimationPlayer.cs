using EidolonCore.ECS;

namespace EidolonCore.Nexus.Animation;

/// <summary>Playback state for a clip: what plays, how fast, and where the playhead is.</summary>
/// <remarks>
/// This is per-instance state, not clip data, so the same clip can run on several entities at different
/// times. Scene values are copied for play mode, which is why the clip reference is shared but the
/// playhead is not.
/// </remarks>
public sealed class AnimationPlayer : IComponent
{
    /// <summary>The clip to play, or null when the player has nothing assigned yet.</summary>
    public AnimationClip? Clip { get; set; }

    /// <summary>Playhead in seconds. Wrapped by the loop mode, never moved by anything but the system.</summary>
    public float Time { get; set; }

    /// <summary>Time scale: one is real time, negative plays backwards.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Whether the playhead wraps at the end instead of stopping on the last pose.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Whether the playhead advances. A paused player still publishes its pose.</summary>
    public bool Playing { get; set; } = true;

    public AnimationPlayer()
    {
    }

    /// <summary>Copies the player for a play-mode snapshot: same clip, independent playhead.</summary>
    public AnimationPlayer(AnimationPlayer other)
    {
        Clip = other.Clip;
        Time = other.Time;
        Speed = other.Speed;
        Loop = other.Loop;
        Playing = other.Playing;
    }
}
