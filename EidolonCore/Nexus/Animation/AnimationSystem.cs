using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace EidolonCore.Nexus.Animation;

/// <summary>Advances authored clips and publishes their poses as desired state.</summary>
/// <remarks>
/// <para>
/// A player is authored data on an entity; the system derives the bindings from its clip and drives the
/// playhead. Sampled poses go to <see cref="DesiredPose"/>, never directly into the transform, so
/// something else can still outrank animation before <see cref="PoseResolutionSystem"/> commits it.
/// </para>
/// <para>
/// Bindings are re-resolved when the world instance changes (play mode clones the world) or when the set
/// of players, or the clip a player holds, changes. Renaming bones or reparenting them mid-session does
/// not rebind, which keeps the per-frame path lookup out of the loop.
/// </para>
/// </remarks>
public sealed class AnimationSystem : ISystem
{
    private readonly List<TrackedPlayer> _players = [];
    private readonly List<(Entity Entity, AnimationClip? Clip)> _signature = [];

    private World? _world;

    /// <summary>Players that currently have a clip and at least one resolved binding.</summary>
    public int PlayerCount => _players.Count;

    /// <summary>Path bindings resolved across every player.</summary>
    public int BindingCount
    {
        get
        {
            var count = 0;

            foreach (var player in _players)
                count += player.Bindings.Count;

            return count;
        }
    }

    public void Update(World world, in FrameTime time)
    {
        Sync(world);

        foreach (var tracked in _players)
        {
            if (!world.TryGetComponent<AnimationPlayer>(tracked.Entity, out var player))
                continue;

            if (player.Clip is not { } clip)
                continue;

            if (player.Playing && player.Speed != 0f)
            {
                player.Time += time.Delta * player.Speed;

                if (!player.Loop && player.Time >= clip.Duration)
                {
                    // A one-shot stops on its last pose instead of holding a playhead past the end.
                    player.Time = clip.Duration;
                    player.Playing = false;
                }
            }

            var sampleTime = Wrap(player.Time, clip.Duration, player.Loop);

            foreach (var binding in tracked.Bindings)
            {
                if (binding.Track.TrySample(sampleTime, out var pose))
                    world.SetComponent(binding.Entity, new DesiredPose(pose));
            }
        }
    }

    private static float Wrap(float time, float duration, bool loop)
    {
        if (!loop)
            return System.Math.Clamp(time, 0f, duration);

        var wrapped = time % duration;
        return wrapped < 0f ? wrapped + duration : wrapped;
    }

    private void Sync(World world)
    {
        if (!ReferenceEquals(world, _world))
        {
            Rebuild(world);
            return;
        }

        var index = 0;

        foreach (var entity in world.GetEntitiesWith<Transform, AnimationPlayer>())
        {
            if (index >= _signature.Count)
            {
                Rebuild(world);
                return;
            }

            var clip = world.GetComponent<AnimationPlayer>(entity).Clip;

            if (_signature[index].Entity != entity || !ReferenceEquals(_signature[index].Clip, clip))
            {
                Rebuild(world);
                return;
            }

            index++;
        }

        if (index != _signature.Count)
            Rebuild(world);
    }

    private void Rebuild(World world)
    {
        _players.Clear();
        _signature.Clear();

        foreach (var entity in world.GetEntitiesWith<Transform, AnimationPlayer>())
        {
            var player = world.GetComponent<AnimationPlayer>(entity);
            _signature.Add((entity, player.Clip));

            if (player.Clip is not { } clip)
                continue;

            var bindings = AnimationBindings.Resolve(world, entity, clip);

            if (bindings.Count > 0)
                _players.Add(new TrackedPlayer(entity, bindings));
        }

        _world = world;
    }

    private sealed class TrackedPlayer(Entity entity, List<AnimationBinding> bindings)
    {
        public Entity Entity { get; } = entity;

        public List<AnimationBinding> Bindings { get; } = bindings;
    }
}
