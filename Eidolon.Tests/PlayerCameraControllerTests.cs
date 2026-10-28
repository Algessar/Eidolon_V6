using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
using EidolonGame;

namespace Eidolon.Tests;

public class PlayerCameraControllerTests
{
    private const float Dt = 0.5f;

    private static (World World, Entity Camera, ScriptHost Host, SceneSession Session) Create()
    {
        var world = new World();

        // Looking down -Z from (0, 2, 7), so "forward" is unambiguous in the assertions.
        var camera = SceneSpawner.SpawnCamera(world, new Vector3(0f, 2f, 7f), new Vector3(0f, 2f, 0f), CameraKind.Game);

        var session = new SceneSession(world);
        session.EnterPlay(world);

        var host = new ScriptHost(session);
        host.Add(camera, new PlayerCameraController());

        return (world, camera, host, session);
    }

    private static GameInput Keys(GameKey keys) =>
        new(Vector2.Zero, Vector2.Zero, 0f, false, false, false, keys);

    private static Vector3 Position(World world, Entity entity) =>
        world.GetComponent<Transform>(entity).GetWorldPosition(world);

    [Fact]
    public void ForwardMovesAlongTheViewAndStaysLevel()
    {
        var (world, camera, host, session) = Create();
        session.SetInput(Keys(GameKey.W));

        host.Update(world, new FrameTime(Dt, 0f, 1));

        var position = Position(world, camera);
        Assert.True(position.Z < 7f - 1f, $"expected the camera to move forward, was {position}");
        Assert.Equal(2f, position.Y, 3);
    }

    [Fact]
    public void StrafeMovesAlongTheViewRight()
    {
        var (world, camera, host, session) = Create();
        session.SetInput(Keys(GameKey.D));

        host.Update(world, new FrameTime(Dt, 0f, 1));

        var position = Position(world, camera);
        Assert.True(position.X > 1f, $"expected the camera to move right, was {position}");
        Assert.Equal(7f, position.Z, 3);
    }

    [Fact]
    public void VerticalKeysChangeHeight()
    {
        var (world, camera, host, session) = Create();
        session.SetInput(Keys(GameKey.E));

        host.Update(world, new FrameTime(Dt, 0f, 1));

        Assert.True(Position(world, camera).Y > 2.5f);

        session.SetInput(Keys(GameKey.Q));
        host.Update(world, new FrameTime(Dt, 0f, 2));

        Assert.True(Position(world, camera).Y < 2.5f);
    }

    [Fact]
    public void SprintCoversMoreGround()
    {
        var (world, camera, host, session) = Create();

        session.SetInput(Keys(GameKey.W));
        host.Update(world, new FrameTime(Dt, 0f, 1));
        var walked = 7f - Position(world, camera).Z;

        var (sprintWorld, sprintCamera, sprintHost, sprintSession) = Create();
        sprintSession.SetInput(Keys(GameKey.W | GameKey.Shift));
        sprintHost.Update(sprintWorld, new FrameTime(Dt, 0f, 1));
        var sprinted = 7f - Position(sprintWorld, sprintCamera).Z;

        Assert.True(sprinted > walked * 2f, $"sprint covered {sprinted} against a walk of {walked}");
    }

    [Fact]
    public void RightDragTurnsTheView()
    {
        var (world, camera, host, session) = Create();

        host.Update(world, new FrameTime(Dt, 0f, 1));
        var before = Position(world, camera);

        session.SetInput(new GameInput(Vector2.Zero, new Vector2(100f, 0f), 0f, false, true, false, GameKey.None));
        host.Update(world, new FrameTime(Dt, 0f, 2));

        var rotation = world.GetComponent<Transform>(camera).GetWorldRotation(world);
        var forward = CameraOrientation.ForwardFrom(rotation);

        // Dragging right looks right, and looking must not slide the camera.
        Assert.True(forward.X > 0.3f, $"expected the view to turn right, forward was {forward}");
        VectorAssert.Near(before, Position(world, camera));
    }

    [Fact]
    public void NoInputLeavesTheCameraAlone()
    {
        var (world, camera, host, session) = Create();
        session.SetInput(GameInput.None);

        var before = world.GetComponent<Transform>(camera);
        host.Update(world, new FrameTime(Dt, 0f, 1));

        var after = world.GetComponent<Transform>(camera);
        VectorAssert.Near(before.Position, after.Position);
        Assert.Equal(before.Rotation, after.Rotation);
    }
}
