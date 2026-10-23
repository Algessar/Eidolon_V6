using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class SpinSystemTests
{
    private sealed class ThrowingSystem : ISystem
    {
        public void Update(World world, in FrameTime time) => throw new InvalidOperationException("boom");
    }

    private static Entity Spinner(World world, float radiansPerSecond, Vector3 axis)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, Transform.Identity);
        world.SetComponent(entity, new Spin { Axis = axis, RadiansPerSecond = radiansPerSecond });
        return entity;
    }

    [Fact]
    public void SpinRotatesByAxisAndSpeed()
    {
        var world = new World();
        var entity = Spinner(world, MathF.PI, Vector3.UnitY);

        new SpinSystem().Update(world, new FrameTime(0.5f, 0.5f, 1));

        // Half a second at pi rad/s is a quarter turn around Y.
        var rotation = world.GetComponent<Transform>(entity).Rotation;
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        var actual = Vector3.Transform(Vector3.UnitX, rotation);
        var expectedDirection = Vector3.Transform(Vector3.UnitX, expected);

        VectorAssert.Near(expectedDirection, actual);
    }

    [Fact]
    public void EntitiesWithoutSpinAreUntouched()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, Transform.Identity);

        new SpinSystem().Update(world, new FrameTime(1f, 1f, 1));

        Assert.Equal(Quaternion.Identity, world.GetComponent<Transform>(entity).Rotation);
    }

    [Fact]
    public void ZeroAxisOrSpeedIsIgnored()
    {
        var world = new World();
        var stopped = Spinner(world, 0f, Vector3.UnitY);
        var noAxis = Spinner(world, 1f, Vector3.Zero);

        new SpinSystem().Update(world, new FrameTime(1f, 1f, 1));

        Assert.Equal(Quaternion.Identity, world.GetComponent<Transform>(stopped).Rotation);
        Assert.Equal(Quaternion.Identity, world.GetComponent<Transform>(noAxis).Rotation);
    }

    [Fact]
    public void FailingSystemStopsPlayAndReports()
    {
        var session = new SceneSession(new World());
        session.AddSystem(new ThrowingSystem());
        session.EnterPlay(new World());

        session.Tick(0.1f);

        Assert.Equal(PlayState.Stopped, session.State);
        Assert.False(session.InPlayMode);
        Assert.NotNull(session.LastError);
        Assert.Contains("boom", session.LastError!.Message);
    }

    [Fact]
    public void ErrorClearsWhenPlayStartsAgain()
    {
        var session = new SceneSession(new World());
        session.AddSystem(new ThrowingSystem());
        session.EnterPlay(new World());
        session.Tick(0.1f);

        session.EnterPlay(new World());

        Assert.Null(session.LastError);
        Assert.Equal(PlayState.Playing, session.State);
    }
}
