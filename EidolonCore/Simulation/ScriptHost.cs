using EidolonCore.ECS;

namespace EidolonCore.Simulation;

/// <summary>Per-entity gameplay behaviour, ticked by a <see cref="ScriptHost"/>.</summary>
/// <remarks>
/// Scripts are plain objects rather than components because the ECS stores components per concrete
/// type, so a component base class could not be enumerated. The host owns the entity/script pairs and
/// drives their lifecycle instead.
/// </remarks>
public interface IScript
{
    /// <summary>
    /// Called once per run, before that run's first update. Play mode clones the authored world, so a
    /// script must reset its own state here rather than in a constructor.
    /// </summary>
    void Start(Entity self, World world);

    /// <summary>Called once per simulation step.</summary>
    void Update(Entity self, World world, in FrameTime time, in GameInput input);
}

/// <summary>Ticks the registered scripts, and gives them the session's current input.</summary>
/// <remarks>
/// A new world instance means a new run: scripts are rebound by entity id (which the play-mode clone
/// preserves) and every one of them gets <see cref="IScript.Start"/> again.
/// </remarks>
public sealed class ScriptHost : ISystem
{
    private readonly SceneSession _session;
    private readonly List<(Entity Entity, IScript Script)> _scripts = [];

    private World? _runningWorld;

    public ScriptHost(SceneSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public int ScriptCount => _scripts.Count;

    /// <summary>Binds a script to an entity. A script may be ticked for at most one entity.</summary>
    public void Add(Entity entity, IScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _scripts.Add((entity, script));

        // Added mid-run: that run has already started, so this script starts now.
        if (_runningWorld is not null &&
            ReferenceEquals(_runningWorld, _session.Active) &&
            _runningWorld.IsAlive(entity))
        {
            script.Start(entity, _runningWorld);
        }
    }

    /// <summary>Removes every script bound to the entity, returning how many were removed.</summary>
    public int Remove(Entity entity) => _scripts.RemoveAll(entry => entry.Entity == entity);

    public void Update(World world, in FrameTime time)
    {
        if (!ReferenceEquals(world, _runningWorld))
        {
            _runningWorld = world;

            foreach (var (entity, script) in _scripts)
            {
                if (world.IsAlive(entity))
                    script.Start(entity, world);
            }
        }

        // Input is per frame, not per step, so every step of the same frame sees the same value.
        var input = _session.Input;

        foreach (var (entity, script) in _scripts)
        {
            if (world.IsAlive(entity))
                script.Update(entity, world, time, input);
        }
    }
}
