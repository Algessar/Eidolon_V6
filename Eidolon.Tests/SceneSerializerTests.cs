using System.Numerics;
using Eidolon.Editor;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public sealed class SceneSerializerTests : IDisposable
{
    private readonly string _path;
    private readonly AssetLibrary _assets = new();

    public SceneSerializerTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "eidolon-scene-" + Guid.NewGuid().ToString("N"), "scene.json");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null && Directory.Exists(directory))
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static World SampleWorld()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(1, 2, 3)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(4, 5, 6), Quaternion.Identity, new Vector3(2f))
        {
            Parent = parent,
        });
        world.SetComponent(child, new MeshRenderer(PrimitiveMeshes.Cube,
            new Material("Red Plastic", new Vector4(0.8f, 0.2f, 0.3f, 1f))));
        world.SetComponent(child, new Spin { Axis = Vector3.UnitZ, RadiansPerSecond = 2.5f });

        var camera = world.CreateEntity();
        world.SetComponent(camera, new Transform(new Vector3(3, 2, 5)));
        var cameraComponent = new Camera { Kind = CameraKind.Game, Position = new Vector3(3, 2, 5) };
        cameraComponent.SetTarget(new Vector3(0, 0.5f, 0));
        world.SetComponent(camera, cameraComponent);

        return world;
    }

    [Fact]
    public void RoundTripPreservesEntitiesTransformsAndComponents()
    {
        var source = SampleWorld();
        SceneSerializer.Save(source, _path);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets);

        Assert.Equal(source.EntityCount, loaded.EntityCount);

        foreach (var entity in source.Entities)
        {
            Assert.True(loaded.IsAlive(entity), $"entity {entity.Id} missing after load");
        }

        var child = source.Entities.OrderBy(entity => entity.Id).Skip(1).First();
        var original = source.GetComponent<Transform>(child);
        var restored = loaded.GetComponent<Transform>(child);

        VectorAssert.Near(original.Position, restored.Position);
        VectorAssert.Near(original.Scale, restored.Scale);
        Assert.Equal(original.Parent, restored.Parent);

        var renderer = loaded.GetComponent<MeshRenderer>(child);
        Assert.Same(PrimitiveMeshes.Cube, renderer.Mesh);
        Assert.Equal("Red Plastic", renderer.Material.Name);
        Assert.Equal(0.8f, renderer.Material.BaseColor.X, 4);
        Assert.Equal(0.2f, renderer.Material.BaseColor.Y, 4);
        Assert.Equal(0.3f, renderer.Material.BaseColor.Z, 4);
        Assert.Equal(1f, renderer.Material.BaseColor.W, 4);

        var spin = loaded.GetComponent<Spin>(child);
        VectorAssert.Near(Vector3.UnitZ, spin.Axis);
        Assert.Equal(2.5f, spin.RadiansPerSecond, 4);
    }

    [Fact]
    public void CameraRoundTripsWithItsKind()
    {
        SceneSerializer.Save(SampleWorld(), _path);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets);

        var cameraEntity = loaded.GetEntitiesWith<Camera>().Single();
        var camera = loaded.GetComponent<Camera>(cameraEntity);

        Assert.Equal(CameraKind.Game, camera.Kind);
        VectorAssert.Near(new Vector3(3, 2, 5), camera.Position);
        VectorAssert.Near(Vector3.Normalize(new Vector3(0, 0.5f, 0) - new Vector3(3, 2, 5)),
            Vector3.Normalize(camera.Target - camera.Position));
    }

    [Fact]
    public void LoadingReplacesTheExistingScene()
    {
        SceneSerializer.Save(SampleWorld(), _path);

        var target = new World();
        var stale = target.CreateEntity();
        target.SetComponent(stale, Transform.Identity);

        SceneSerializer.Load(_path, target, _assets);

        // Ids are preserved, so the stale entity may be reused by the loaded scene; the count is
        // what proves the old contents were dropped.
        Assert.Equal(3, target.EntityCount);
    }

    [Fact]
    public void UnknownMeshLoadsTheEntityWithoutAMesh()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, Transform.Identity);
        world.SetComponent(entity, new MeshRenderer(PrimitiveMeshes.Cube, new Material("Lost")));

        SceneSerializer.Save(world, _path);

        // Break the reference so the name cannot be resolved.
        var json = File.ReadAllText(_path).Replace("\"Cube\"", "\"NotAMesh\"");
        File.WriteAllText(_path, json);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets);

        Assert.Null(loaded.GetComponent<MeshRenderer>(entity).Mesh);
        Assert.Equal("Lost", loaded.GetComponent<MeshRenderer>(entity).Material.Name);
    }

    [Fact]
    public void MissingFileThrows()
    {
        Assert.Throws<FileNotFoundException>(() =>
            SceneSerializer.Load(Path.Combine(Path.GetTempPath(), "eidolon-missing-scene.json"), new World(), _assets));
    }

    [Fact]
    public void ViewportMetadataRoundTrips()
    {
        var viewport = new Eidolon.Editor.ViewportSettings
        {
            ShowGrid = false,
            GridHeight = 1.25f,
            GridExtent = 8f,
            GridSpacing = 0.5f,
        };

        SceneSerializer.Save(SampleWorld(), _path, viewport);

        var loaded = new Eidolon.Editor.ViewportSettings();
        SceneSerializer.Load(_path, new World(), _assets, loaded);

        Assert.False(loaded.ShowGrid);
        Assert.Equal(1.25f, loaded.GridHeight, 3);
        Assert.Equal(8f, loaded.GridExtent, 3);
        Assert.Equal(0.5f, loaded.GridSpacing, 3);
    }
}
