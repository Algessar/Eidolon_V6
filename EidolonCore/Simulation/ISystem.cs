using EidolonCore.ECS;

namespace EidolonCore.Simulation;

/// <summary>
/// One simulation step's timing. Simulation time only advances while play mode is running, so it
/// is not the same thing as wall-clock time.
/// </summary>
/// <param name="Delta">Seconds since the previous step.</param>
/// <param name="Total">Seconds of simulation elapsed since play started.</param>
/// <param name="FrameNumber">How many steps have run since play started.</param>
public readonly record struct FrameTime(float Delta, float Total, long FrameNumber);

/// <summary>
/// A gameplay system: pure world mutation, driven by <see cref="SceneSession.Tick"/>.
/// </summary>
/// <remarks>
/// Systems only see the active world (the runtime copy while playing). They do not receive input
/// or rendering state; those get their own contracts when gameplay needs them.
/// </remarks>
public interface ISystem
{
    void Update(World world, in FrameTime time);
}
