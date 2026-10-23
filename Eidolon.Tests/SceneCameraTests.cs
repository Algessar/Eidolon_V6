using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;

namespace Eidolon.Tests;

public class SceneCameraTests
{
    private static Entity CameraEntity(World world, Vector3 position, Vector3 target)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position));

        var camera = new Camera { Position = position };
        camera.SetTarget(target);
        world.SetComponent(entity, camera);
        return entity;
    }

    [Fact]
    public void MovingTheTransformMovesTheCamera()
    {
        var world = new World();
        var entity = CameraEntity(world, Vector3.Zero, new Vector3(0, 0, -1));

        world.GetComponent<Camera>(entity).Position = Vector3.Zero;
        world.GetComponent<Camera>(entity).Target = new Vector3(0, 0, -1);

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Position = new Vector3(5, 0, 0);

        SceneCameras.SyncFromTransforms(world);

        var camera = world.GetComponent<Camera>(entity);
        VectorAssert.Near(new Vector3(5, 0, 0), camera.Position);
    }

    [Fact]
    public void MovingKeepsTheLookDirection()
    {
        var world = new World();
        var entity = CameraEntity(world, Vector3.Zero, new Vector3(0, 0, -1));

        var before = Vector3.Normalize(world.GetComponent<Camera>(entity).Target - world.GetComponent<Camera>(entity).Position);

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Position = new Vector3(3, 2, 1);

        SceneCameras.SyncFromTransforms(world);

        var camera = world.GetComponent<Camera>(entity);
        var after = Vector3.Normalize(camera.Target - camera.Position);
        VectorAssert.Near(before, after);
    }

    [Fact]
    public void NestedCameraUsesItsWorldPosition()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(10, 0, 0)));

        var entity = CameraEntity(world, new Vector3(1, 0, 0), new Vector3(1, 0, -1));
        Assert.True(Hierarchy.TrySetParent(world, entity, parent));

        SceneCameras.SyncFromTransforms(world);

        // Parenting preserved the world position, so the camera should stay where it was.
        VectorAssert.Near(new Vector3(1, 0, 0), world.GetComponent<Camera>(entity).Position);
    }

    [Fact]
    public void SyncIsIdempotent()
    {
        var world = new World();
        var entity = CameraEntity(world, new Vector3(2, 1, 0), new Vector3(2, 1, -1));

        SceneCameras.SyncFromTransforms(world);
        var camera = world.GetComponent<Camera>(entity);
        var target = camera.Target;

        SceneCameras.SyncFromTransforms(world);

        Assert.Equal(target, world.GetComponent<Camera>(entity).Target);
    }
}
