using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;
using EidolonCore.Scene;
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
        world.SetComponent(child, new XpbdBody
        {
            Shape = XpbdShape.Capsule,
            Radius = 0.25f,
            SegmentLength = 0.75f,
            InverseMass = 0f,
        });

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

        var body = loaded.GetComponent<XpbdBody>(child);
        Assert.Equal(XpbdShape.Capsule, body.Shape);
        Assert.Equal(0.25f, body.Radius, 4);
        Assert.Equal(0.75f, body.SegmentLength, 4);
        Assert.Equal(0f, body.InverseMass);
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
    public void NamesAndAnimationRoundTrip()
    {
        var world = new World();

        var root = world.CreateEntity();
        world.SetComponent(root, new Transform(new Vector3(1f, 2f, 3f)));
        world.SetComponent(root, new Name("Windmill"));

        var clip = new AnimationClip("Spin", 2f,
        [
            new AnimationTrack("Rotor",
            [
                new PoseKey(0f, LocalPose.Identity),
                new PoseKey(2f, new LocalPose(new Vector3(0f, 1f, 0f),
                    Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f), new Vector3(2f))),
            ]),
        ]);

        world.SetComponent(root, new AnimationPlayer
        {
            Clip = clip,
            Time = 0.75f,
            Speed = 0.5f,
            Loop = false,
            Playing = true,
        });

        SceneSerializer.Save(world, _path);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets);

        Assert.Equal("Windmill", loaded.GetComponent<Name>(root).Value);

        var player = loaded.GetComponent<AnimationPlayer>(root);

        Assert.Equal(0.75f, player.Time, 4);
        Assert.Equal(0.5f, player.Speed, 4);
        Assert.False(player.Loop);
        Assert.True(player.Playing);

        var restored = player.Clip;
        Assert.NotNull(restored);
        Assert.Equal("Spin", restored!.Name);
        Assert.Equal(2f, restored.Duration, 4);
        Assert.Single(restored.Tracks);

        var track = restored.Tracks[0];
        Assert.Equal("Rotor", track.Path);
        Assert.Equal(2, track.Keys.Count);
        VectorAssert.Near(new Vector3(0f, 1f, 0f), track.Keys[1].Pose.Position);
        VectorAssert.Near(new Vector3(2f), track.Keys[1].Pose.Scale);
    }

    [Fact]
    public void ImportedModelsComeBackFromTheirFile()
    {
        var source = ModelFile();
        var model = SampleModel(source);
        var importer = new FakeModelImporter();
        importer.Models[source] = model;

        var authored = new World();
        var root = ModelSpawner.Spawn(authored, model, new Material("Dancer", new Vector4(0.4f, 0.7f, 0.9f, 1f)),
            new Vector3(4f, 0f, 1f));

        var player = authored.GetComponent<AnimationPlayer>(root);
        player.Time = 0.25f;
        player.Loop = false;
        authored.SetComponent(root, player);

        var nodeIds = authored.Entities.OrderBy(entity => entity.Id).ToArray();

        SceneSerializer.Save(authored, _path);

        // The scene points at the file; it does not carry the geometry or the clip.
        var json = File.ReadAllText(_path);
        Assert.Contains(source.Replace("\\", "\\\\"), json);
        Assert.DoesNotContain("\"Tracks\"", json);
        Assert.DoesNotContain("\"Vertices\"", json);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets, models: importer);

        Assert.Equal(authored.EntityCount, loaded.EntityCount);

        foreach (var id in nodeIds)
            Assert.True(loaded.IsAlive(id), $"entity {id} was not restored");

        // Parent links were rebuilt by the spawn, not by re-reading ids out of the file.
        var sword = loaded.GetEntitiesWith<MeshRenderer>()
            .Single(entity => loaded.GetComponent<Name>(entity).Value == "Sword");
        Assert.Equal(root, loaded.GetComponent<Transform>(sword).Parent);

        // The geometry is the one the importer produced, and the material came out of the scene.
        Assert.Same(model.Nodes[2].Mesh, loaded.GetComponent<MeshRenderer>(sword).Mesh);
        Assert.Equal("Dancer", loaded.GetComponent<MeshRenderer>(sword).Material.Name);
        Assert.Equal(0.4f, loaded.GetComponent<MeshRenderer>(sword).Material.BaseColor.X, 4);

        // The clip is picked again by name from the file, with the playhead where it was left.
        var restored = loaded.GetComponent<AnimationPlayer>(root);
        Assert.Same(model.Clips[0], restored.Clip);
        Assert.Equal(0.25f, restored.Time, 4);
        Assert.False(restored.Loop);
    }

    [Fact]
    public void WithoutAnImporterTheImportedEntitiesStillLoad()
    {
        var source = ModelFile();
        var model = SampleModel(source);

        var authored = new World();
        var root = ModelSpawner.Spawn(authored, model, new Material("Dancer"), Vector3.Zero);

        SceneSerializer.Save(authored, _path);

        var loaded = new World();
        SceneSerializer.Load(_path, loaded, _assets);

        // A headless load has no importer: the hierarchy and its names come back, the geometry does not.
        Assert.Equal(authored.EntityCount, loaded.EntityCount);

        var sword = loaded.GetEntitiesWith<MeshRenderer>()
            .Single(entity => loaded.GetComponent<Name>(entity).Value == "Sword");
        Assert.Null(loaded.GetComponent<MeshRenderer>(sword).Mesh);

        Assert.True(loaded.IsAlive(root));
    }

    /// <summary>A dummy model file: the test's importer answers by path, so only existence matters.</summary>
    private string ModelFile()
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        var source = Path.Combine(directory, "model.fbx");
        File.WriteAllText(source, "not really an fbx");

        return Path.GetFullPath(source);
    }

    [Fact]
    public void SpawnedModelsRecordWhereTheyCameFrom()
    {
        var source = Path.Combine(Path.GetTempPath(), $"eidolon-model-{Guid.NewGuid():N}.fbx");
        var world = new World();

        var root = ModelSpawner.Spawn(world, SampleModel(source), new Material("Dancer"), Vector3.Zero);

        var origins = world.GetEntitiesWith<ModelOrigin>()
            .OrderBy(entity => world.GetComponent<ModelOrigin>(entity).NodeIndex)
            .ToArray();

        Assert.Equal(3, origins.Length);
        Assert.All(origins, entity => Assert.Equal(root, world.GetComponent<ModelOrigin>(entity).Root));
        Assert.Equal([0, 1, 2], origins.Select(entity => world.GetComponent<ModelOrigin>(entity).NodeIndex));

        Assert.Equal(source, world.GetComponent<ModelOrigin>(root).SourcePath);
        Assert.Null(world.GetComponent<ModelOrigin>(origins[1]).SourcePath);
    }

    /// <summary>An importer that answers with models built in the test, so no file has to exist.</summary>
    private sealed class FakeModelImporter : IModelImporter
    {
        public Dictionary<string, ModelAsset> Models { get; } = new(StringComparer.Ordinal);

        public ModelAsset? Import(string path) => Models.GetValueOrDefault(path);
    }

    private static ModelAsset SampleModel(string source) => new()
    {
        Name = "Dancer",
        SourcePath = source,
        Nodes =
        [
            new ModelNode { Name = "Root", Children = [1, 2] },
            new ModelNode { Name = "Spine", Position = new Vector3(0f, 1f, 0f) },
            new ModelNode { Name = "Sword", Mesh = new Mesh("Sword", [new Vertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero)], [0, 0, 0]) },
        ],
        Clips =
        [
            new AnimationClip("Idle", 1f,
            [
                new AnimationTrack("Spine",
                [
                    new PoseKey(0f, LocalPose.Identity),
                    new PoseKey(1f, LocalPose.Identity with
                    {
                        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
                    }),
                ]),
            ]),
        ],
    };

    [Fact]
    public void ViewportMetadataRoundTrips()
    {
        var viewport = new ViewportSettings
        {
            ShowGrid = false,
            GridHeight = 1.25f,
            GridExtent = 8f,
            GridSpacing = 0.5f,
        };

        SceneSerializer.Save(SampleWorld(), _path, viewport);

        var loaded = new ViewportSettings();
        SceneSerializer.Load(_path, new World(), _assets, loaded);

        Assert.False(loaded.ShowGrid);
        Assert.Equal(1.25f, loaded.GridHeight, 3);
        Assert.Equal(8f, loaded.GridExtent, 3);
        Assert.Equal(0.5f, loaded.GridSpacing, 3);
    }
}
