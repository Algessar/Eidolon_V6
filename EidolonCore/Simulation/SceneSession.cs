using EidolonCore.ECS;

namespace EidolonCore.Simulation;

/// <summary>Whether the authored world is being simulated, and if so whether it is running.</summary>
public enum PlayState
{
    /// <summary>Editing: the authored world is the one being shown and modified.</summary>
    Stopped,

    /// <summary>A runtime copy is being simulated.</summary>
    Playing,

    /// <summary>A runtime copy exists but is frozen.</summary>
    Paused,
}

/// <summary>
/// Holds the authored world and, while play mode runs, the runtime copy that is actually being
/// simulated and drawn. Everything that renders or edits the scene reads <see cref="Active"/>
/// instead of keeping its own world reference, which is what lets play mode swap worlds.
/// </summary>
/// <remarks>
/// The session deliberately does not know how to copy a world: the runtime world is supplied by
/// the caller (currently nobody), so the component-copy rules can be designed without changing
/// this type. <see cref="Stop"/> always discards the runtime world, never the authored one.
/// </remarks>
public sealed class SceneSession
{
    public SceneSession(World authored)
    {
        ArgumentNullException.ThrowIfNull(authored);
        Authored = authored;
    }

    /// <summary>The world the editor authored. Never replaced, and disposed of with the session.</summary>
    public World Authored { get; }

    /// <summary>The world play mode is simulating, or null while stopped.</summary>
    public World? Runtime { get; private set; }

    public PlayState State { get; private set; } = PlayState.Stopped;

    /// <summary>The world to render and edit right now: the runtime copy while playing, else authored.</summary>
    public World Active => Runtime ?? Authored;

    /// <summary>True while a runtime world is running, paused or not.</summary>
    public bool InPlayMode => Runtime is not null;

    /// <summary>
    /// Switches the session to the supplied runtime world. The caller owns how that world was
    /// produced (a clone, a loaded scene, a hand-built test world).
    /// </summary>
    /// <exception cref="InvalidOperationException">Play mode is already running.</exception>
    public void EnterPlay(World runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (Runtime is not null)
            throw new InvalidOperationException("Play mode is already running.");

        Runtime = runtime;
        State = PlayState.Playing;
        _time = default;
        LastError = null;
    }

    private readonly List<ISystem> _systems = [];
    private FrameTime _time;

    /// <summary>Gameplay systems ticked while play mode is running.</summary>
    public IReadOnlyList<ISystem> Systems => _systems;

    /// <summary>Simulation time of the current play session.</summary>
    public FrameTime Time => _time;

    /// <summary>Exception thrown by a system during the last tick, if any.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Input delivered to gameplay this frame; always <see cref="GameInput.None"/> while stopped.</summary>
    public GameInput Input { get; private set; }

    /// <summary>
    /// Publishes this frame's gameplay input. Ignored unless the session is playing, and cleared
    /// whenever the game view does not have the cursor.
    /// </summary>
    public void SetInput(in GameInput input) => Input = State == PlayState.Stopped ? GameInput.None : input;

    /// <summary>Drops any input, for example when the game view loses focus.</summary>
    public void ClearInput() => Input = GameInput.None;
    public void AddSystem(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _systems.Add(system);
    }

    /// <summary>Simulation step length, in seconds. Defaults to 60 Hz.</summary>
    public float FixedDelta { get; set; } = 1f / 60f;

    /// <summary>
    /// Upper bound on simulation steps per frame, so a long stall cannot make the editor spend the
    /// next frames catching up (the "spiral of death").
    /// </summary>
    public int MaxStepsPerTick { get; set; } = 8;

    private float _accumulator;

    /// <summary>
    /// Feeds one frame of wall-clock time into the fixed-step accumulator. Does nothing unless the
    /// session is playing, so pause is simply "stop ticking" and stopping drops the world the
    /// systems were writing to.
    /// </summary>
    public void Tick(float delta)
    {
        if (State != PlayState.Playing)
            return;

        if (delta > 0f)
            _accumulator += delta;

        var fixedDelta = FixedDelta > 1e-5f ? FixedDelta : 1e-5f;
        var maxSteps = System.Math.Max(1, MaxStepsPerTick);
        var steps = 0;

        while (_accumulator >= fixedDelta && steps < maxSteps)
        {
            _accumulator -= fixedDelta;
            steps++;

            if (!Step(fixedDelta))
                return;
        }

        // Time we could not simulate is dropped rather than queued up for later.
        if (steps == maxSteps)
            _accumulator = 0f;
    }

    /// <summary>Runs exactly one simulation step. Returns false when a system failed.</summary>
    private bool Step(float fixedDelta)
    {
        _time = new FrameTime(fixedDelta, _time.Total + fixedDelta, _time.FrameNumber + 1);

        foreach (var system in _systems)
        {
            try
            {
                system.Update(Active, _time);
            }
            catch (Exception exception)
            {
                // A failing system must not leave the editor half in play mode: stop and report.
                LastError = exception;
                Stop();
                return false;
            }
        }

        return true;
    }

    /// <summary>Freezes the runtime world. Does nothing unless it is playing.</summary>
    public void Pause()
    {
        if (State == PlayState.Playing)
            State = PlayState.Paused;
    }

    /// <summary>Unfreezes the runtime world. Does nothing unless it is paused.</summary>
    public void Resume()
    {
        if (State == PlayState.Paused)
            State = PlayState.Playing;
    }

    /// <summary>Drops the runtime world and returns to the authored one.</summary>
    public void Stop()
    {
        Runtime = null;
        State = PlayState.Stopped;
        _time = default;
        Input = GameInput.None;
    }
}
