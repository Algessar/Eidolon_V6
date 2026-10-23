using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class SceneSpawnerTests
{
    private static Mesh WideTriangle()
    {
        Vertex[] vertices =
        [
            new(new Vector3(-2, 0, 0), Vector3.UnitY),
            new(new Vector3(2, 0, 0), Vector3.UnitY),
            new(new Vector3(0, 0, 2), Vector3.UnitY),
        ];

        return new Mesh("wide", vertices, [0, 1, 2]);
    }

    [Fact]
    public void SpawnMeshAddsTransformAndRenderer()
    {
        var world = new World();
        var mesh = PrimitiveMeshes.Cube;
        var material = new Material("Test", new Vector4(1, 0, 0, 1));

        var entity = SceneSpawner.SpawnMesh(world, mesh, material, new Vector3(1, 2, 3), Vector3.One);

        Assert.True(world.HasComponent<Transform>(entity));
        Assert.True(world.HasComponent<MeshRenderer>(entity));
        Assert.Equal(mesh, world.GetComponent<MeshRenderer>(entity).Mesh);
        Assert.Equal(material, world.GetComponent<MeshRenderer>(entity).Material);
        Assert.Equal(new Vector3(1, 2, 3), world.GetComponent<Transform>(entity).Position);
    }

    [Fact]
    public void SpawnFittedMeshScalesMeshIntoUnitBox()
    {
        var world = new World();
        var mesh = WideTriangle();

        // The mesh spans 4 units on X, so it must be scaled by 1/4.
        var entity = SceneSpawner.SpawnFittedMesh(world, mesh, new Material(), new Vector3(5, 6, 7));

        var transform = world.GetComponent<Transform>(entity);
        Assert.Equal(0.25f, transform.Scale.X, 5);
        Assert.Equal(0.25f, transform.Scale.Y, 5);
        Assert.Equal(0.25f, transform.Scale.Z, 5);
    }

    [Fact]
    public void SpawnFittedMeshPutsBoundsCentreAtRequestedPoint()
    {
        var world = new World();
        var mesh = WideTriangle();
        var requested = new Vector3(5, 6, 7);

        var entity = SceneSpawner.SpawnFittedMesh(world, mesh, new Material(), requested);

        var transform = world.GetComponent<Transform>(entity);
        var worldCentre = Vector3.Transform(mesh.LocalBounds.Center, transform.GetLocalMatrix());

        VectorAssert.Near(requested, worldCentre);
    }
}
