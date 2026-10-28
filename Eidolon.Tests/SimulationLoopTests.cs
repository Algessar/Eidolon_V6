using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class SimulationLoopTests
{
    private sealed class CountingSystem : ISystem
    {
        public int Calls { get; private set; }
        public FrameTime Last;
        public World? LastWorld;

        public void Update(World world, in FrameTime time)
        {
            Calls++;
            Last = time;
            LastWorld = world;
        }
    }

    [Fact]
    public void SystemsOnlyTickWhilePlaying()
    {
        var authored = new World();
        var session = new SceneSession(authored);
        var system = new CountingSystem();
        session.AddSystem(system);
        session.FixedDelta = 0.1f;

        session.Tick(0.1f);
        Assert.Equal(0, system.Calls);

        session.EnterPlay(new World());
        session.Tick(0.1f);
        session.Tick(0.1f);
        Assert.Equal(2, system.Calls);

        session.Pause();
        session.Tick(0.1f);
        Assert.Equal(2, system.Calls);

        session.Resume();
        session.Tick(0.1f);
        Assert.Equal(3, system.Calls);

        session.Stop();
        session.Tick(0.1f);
        Assert.Equal(3, system.Calls);
    }

    [Fact]
    public void FrameTimeAccumulatesWhilePlaying()
    {
        var session = new SceneSession(new World());
        var system = new CountingSystem();
        session.AddSystem(system);
        session.FixedDelta = 0.25f;

        session.EnterPlay(new World());
        session.Tick(0.25f);
        session.Tick(0.5f);

        Assert.Equal(0.75f, session.Time.Total, 4);
        Assert.Equal(3, session.Time.FrameNumber);
        Assert.Equal(0.25f, system.Last.Delta, 4);
    }

    [Fact]
    public void SystemsSeeTheRuntimeWorld()
    {
        var authored = new World();
        var runtime = new World();
        var session = new SceneSession(authored);
        var system = new CountingSystem();
        session.AddSystem(system);

        session.EnterPlay(runtime);
        session.Tick(0.1f);

        Assert.Same(runtime, system.LastWorld);
    }

    [Fact]
    public void TimeResetsOnEnterAndStop()
    {
        var session = new SceneSession(new World()) { FixedDelta = 0.25f };
        session.EnterPlay(new World());
        session.Tick(1f);
        Assert.Equal(1f, session.Time.Total, 4);

        session.Stop();
        Assert.Equal(0f, session.Time.Total, 4);
        Assert.Equal(0, session.Time.FrameNumber);
    }

    [Fact]
    public void InputIsOnlyDeliveredWhilePlaying()
    {
        var session = new SceneSession(new World());

        var input = new GameInput(new Vector2(10, 20), new Vector2(1, 0), 0f, true, false, false,
            GameKey.W | GameKey.Shift);

        session.SetInput(input);
        Assert.Equal(GameInput.None, session.Input);

        session.EnterPlay(new World());
        session.SetInput(input);
        Assert.Equal(input, session.Input);

        session.Pause();
        session.SetInput(input);
        Assert.Equal(input, session.Input);

        session.ClearInput();
        Assert.Equal(GameInput.None, session.Input);

        session.SetInput(input);
        session.Stop();
        Assert.Equal(GameInput.None, session.Input);
    }

    [Fact]
    public void StepsUseTheFixedDeltaAndCarryLeftoverTime()
    {
        var session = new SceneSession(new World()) { FixedDelta = 0.1f };
        var system = new CountingSystem();
        session.AddSystem(system);
        session.EnterPlay(new World());

        session.Tick(0.25f);   // two whole steps, 0.05 left over
        Assert.Equal(2, system.Calls);
        Assert.Equal(0.1f, system.Last.Delta, 4);

        session.Tick(0.06f);   // leftover plus new time makes a third step
        Assert.Equal(3, system.Calls);
        Assert.Equal(0.3f, session.Time.Total, 4);
    }

    [Fact]
    public void LongStallIsCappedInsteadOfQueueingSteps()
    {
        var session = new SceneSession(new World()) { FixedDelta = 0.1f, MaxStepsPerTick = 3 };
        var system = new CountingSystem();
        session.AddSystem(system);
        session.EnterPlay(new World());

        session.Tick(5f);
        Assert.Equal(3, system.Calls);

        session.Tick(0f);
        Assert.Equal(3, system.Calls);
    }
}
