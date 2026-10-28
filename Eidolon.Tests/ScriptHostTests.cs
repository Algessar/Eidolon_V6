using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class ScriptHostTests
{
    private const float Dt = 1f / 60f;

    private sealed class Recorder : IScript
    {
        public int Starts;
        public int Updates;
        public GameInput LastInput;

        public void Start(Entity self, World world) => Starts++;

        public void Update(Entity self, World world, in FrameTime time, in GameInput input)
        {
            Updates++;
            LastInput = input;
        }
    }

    private static (SceneSession Session, ScriptHost Host, World World, Entity Entity, Recorder Script) Create()
    {
        var world = new World();
        var session = new SceneSession(world);
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(1f, 2f, 3f)));

        var host = new ScriptHost(session);
        var script = new Recorder();
        host.Add(entity, script);

        return (session, host, world, entity, script);
    }

    private static FrameTime Step() => new(Dt, 0f, 1);

    [Fact]
    public void StartRunsOnceBeforeTheFirstUpdate()
    {
        var (_, host, world, _, script) = Create();

        host.Update(world, Step());
        host.Update(world, Step());

        Assert.Equal(1, script.Starts);
        Assert.Equal(2, script.Updates);
    }

    [Fact]
    public void ScriptsSeeTheSessionsInput()
    {
        var (session, host, world, _, script) = Create();
        session.EnterPlay(world);

        var input = new GameInput(new Vector2(4f, 5f), new Vector2(1f, 0f), 0f, false, false, false,
            GameKey.W | GameKey.Shift);

        session.SetInput(input);
        host.Update(world, Step());

        Assert.True(script.LastInput.IsDown(GameKey.W));
        Assert.True(script.LastInput.Shift);
        Assert.False(script.LastInput.IsDown(GameKey.A));
        Assert.Equal(new Vector2(4f, 5f), script.LastInput.MousePosition);
    }

    [Fact]
    public void ANewWorldStartsTheScriptsAgain()
    {
        var (_, host, world, _, script) = Create();

        host.Update(world, Step());

        // Play mode clones the authored world, keeping entity ids, so this is what a restart looks like.
        var clone = world.Clone();
        host.Update(clone, Step());

        Assert.Equal(2, script.Starts);
        Assert.Equal(2, script.Updates);
    }

    [Fact]
    public void ScriptsAreSkippedWhenTheirEntityIsGone()
    {
        var (_, host, world, entity, script) = Create();

        host.Update(world, Step());
        world.DestroyEntity(entity);
        host.Update(world, Step());

        Assert.Equal(1, script.Updates);
    }

    [Fact]
    public void ScriptsAddedMidRunStartImmediately()
    {
        var (session, host, world, _, _) = Create();
        session.EnterPlay(world);
        host.Update(world, Step());

        var late = new Recorder();
        host.Add(world.CreateEntity(), late);

        Assert.Equal(1, late.Starts);
    }

    [Fact]
    public void RemovingAScriptStopsItTicking()
    {
        var (_, host, world, entity, script) = Create();

        host.Update(world, Step());
        Assert.Equal(1, host.Remove(entity));

        host.Update(world, Step());

        Assert.Equal(1, script.Updates);
        Assert.Equal(0, host.ScriptCount);
    }
}
