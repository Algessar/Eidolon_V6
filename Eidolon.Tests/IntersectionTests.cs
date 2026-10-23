using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class IntersectionTests
{
    private static Mesh SingleTriangle() => new("tri",
    [
        new(new Vector3(-1, -1, 0), Vector3.UnitZ),
        new(new Vector3(1, -1, 0), Vector3.UnitZ),
        new(new Vector3(0, 1, 0), Vector3.UnitZ),
    ], [0, 1, 2]);

    [Fact]
    public void RayHitsTriangleInFrontOfIt()
    {
        var mesh = SingleTriangle();

        Assert.True(mesh.TryIntersect(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1)),
            Matrix4x4.Identity, out var distance));

        Assert.Equal(5f, distance, 4);
    }

    [Fact]
    public void RayMissesOutsideTheTriangle()
    {
        var mesh = SingleTriangle();

        Assert.False(mesh.TryIntersect(new Ray(new Vector3(5, 0, 5), new Vector3(0, 0, -1)),
            Matrix4x4.Identity, out _));
    }

    [Fact]
    public void RayPointingAwayDoesNotHit()
    {
        var mesh = SingleTriangle();

        Assert.False(mesh.TryIntersect(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, 1)),
            Matrix4x4.Identity, out _));
    }

    [Fact]
    public void RayHitsCubeFrontFace()
    {
        Assert.True(PrimitiveMeshes.Cube.TryIntersect(
            new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1)), Matrix4x4.Identity, out var distance));

        Assert.Equal(4.5f, distance, 4);
    }

    [Fact]
    public void WorldMatrixMovesTheHit()
    {
        // Pushing the cube 10 units down -Z moves its near face to z = -9.5, so the ray from
        // z = 5 travels 14.5 units before it lands.
        var model = Matrix4x4.CreateTranslation(0, 0, -10);

        Assert.True(PrimitiveMeshes.Cube.TryIntersect(
            new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1)), model, out var distance));

        Assert.Equal(14.5f, distance, 4);
    }

    [Fact]
    public void BoundsRejectRaysThatPassBy()
    {
        var bounds = PrimitiveMeshes.Cube.LocalBounds;

        Assert.True(bounds.Intersects(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1))));
        Assert.False(bounds.Intersects(new Ray(new Vector3(5, 5, 5), new Vector3(0, 0, -1))));
        Assert.False(bounds.Intersects(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, 1))));
    }

    [Fact]
    public void CameraCentreRayPointsAtTheTarget()
    {
        var camera = new Camera { Position = new Vector3(0, 0, 10) };
        camera.SetTarget(Vector3.Zero);
        camera.SetAspectRatio(16, 9);

        var ray = camera.ScreenPointToRay(Vector2.Zero);

        VectorAssert.Near(new Vector3(0, 0, -1), ray.Direction, 1e-4f);
    }

    [Fact]
    public void CameraRayThroughTopOfScreenPointsUpward()
    {
        var camera = new Camera { Position = new Vector3(0, 0, 10) };
        camera.SetTarget(Vector3.Zero);
        camera.SetAspectRatio(16, 9);

        // NDC -1 is the top of the viewport because the projection carries the Vulkan Y flip.
        var ray = camera.ScreenPointToRay(new Vector2(0, -1));

        Assert.True(ray.Direction.Y > 0f);
    }

    [Fact]
    public void PickerReturnsTheNearestEntity()
    {
        var world = new World();
        var near = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), Vector3.Zero, Vector3.One);
        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), new Vector3(0, 0, -10), Vector3.One);

        var picked = ScenePicker.Pick(world, new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1)));

        Assert.Equal(near, picked);
    }

    [Fact]
    public void PickerReturnsNullWhenNothingIsHit()
    {
        var world = new World();
        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), Vector3.Zero, Vector3.One);

        Assert.Null(ScenePicker.Pick(world, new Ray(new Vector3(0, 20, 5), new Vector3(0, 1, 0))));
    }

    [Fact]
    public void PickerUsesTheWorldTransform()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(0, 0, -20)));

        // Authored at world (0,0,-10); parenting keeps it there and stores a local +10 offset.
        var child = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), new Vector3(0, 0, -10), Vector3.One);
        Assert.True(Hierarchy.TrySetParent(world, child, parent));

        VectorAssert.Near(new Vector3(0, 0, 10), world.GetComponent<Transform>(child).Position);
        Assert.Equal(child, ScenePicker.Pick(world, new Ray(Vector3.Zero, new Vector3(0, 0, -1))));
    }

    [Fact]
    public void TryPickReportsTheWorldSpaceHitPoint()
    {
        var world = new World();
        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), Vector3.Zero, Vector3.One);

        var ray = new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1));
        Assert.True(ScenePicker.TryPick(world, ray, out _, out var point));

        // The cube spans -0.5..0.5, so a ray down -Z from +5 hits its +Z face at z = 0.5.
        VectorAssert.Near(new Vector3(0, 0, 0.5f), point);
    }

    [Fact]
    public void TryPickSkipsTheIgnoredEntity()
    {
        var world = new World();
        var ignored = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), Vector3.Zero, Vector3.One);
        var behind = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube, new Material(), new Vector3(0, 0, -5), Vector3.One);

        var ray = new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, -1));

        Assert.True(ScenePicker.TryPick(world, ray, out var hit, out var point, ignored));
        Assert.Equal(behind, hit);
        VectorAssert.Near(new Vector3(0, 0, -4.5f), point);
    }
}
