using System.Numerics;
using Eidolon.Assimp;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class AssimpImportTests : IDisposable
{
    private const string TriangleObj = """
                                      # a single triangle
                                      v 0 0 0
                                      v 1 0 0
                                      v 0 1 0
                                      f 1 2 3
                                      """;

    private const string BvhText = """
                                   HIERARCHY
                                   ROOT Hips
                                   {
                                       OFFSET 0 0 0
                                       CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation
                                       JOINT Spine
                                       {
                                           OFFSET 0 10 0
                                           CHANNELS 3 Zrotation Xrotation Yrotation
                                           End Site
                                           {
                                               OFFSET 0 10 0
                                           }
                                       }
                                   }
                                   MOTION
                                   Frames: 2
                                   Frame Time: 0.04166667
                                   0 0 90 0 0 0  0 0 0
                                   0 0 91 0 0 0  5 0 0
                                   """;

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"eidolon-import-{Guid.NewGuid():N}.obj");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void ImportsGeometryThroughAssimp()
    {
        File.WriteAllText(_path, TriangleObj);

        var model = AssimpModelImporter.Import(_path);

        Assert.NotEmpty(model.Nodes);
        Assert.Equal(Path.GetFileNameWithoutExtension(_path), model.Name);

        var mesh = model.Nodes.Select(node => node.Mesh).FirstOrDefault(candidate => candidate is not null);

        Assert.NotNull(mesh);
        Assert.Equal(3, mesh!.Vertices.Length);
        Assert.Equal(3, mesh.Indices.Length);
        Assert.True(mesh.IsValid);
    }

    [Fact]
    public void ImportedGeometryLandsOnANamedNode()
    {
        File.WriteAllText(_path, TriangleObj);

        var model = AssimpModelImporter.Import(_path);
        // The root is the scene root; the geometry hangs off a child named after the object.
        Assert.Single(model.Nodes[0].Children);
        Assert.False(string.IsNullOrWhiteSpace(model.Nodes[1].Name));
        Assert.Equal(1, model.Nodes[0].Children[0]);
    }

    [Fact]
    public void ImportsAnimationChannelsIncludingRootMotion()
    {
        // Motion capture is the smallest honest way to prove a real animation path: a skeleton plus keyed
        // channels, no geometry and no skinning involved.
        var path = Path.Combine(Path.GetTempPath(), $"eidolon-{Guid.NewGuid():N}.bvh");
        File.WriteAllText(path, BvhText);

        try
        {
            var model = AssimpModelImporter.Import(path);

            Assert.Single(model.Clips);

            var clip = model.Clips[0];

            Assert.True(clip.Duration > 0f);
            Assert.Contains(model.Nodes, node => node.Name == "Spine");

            var spine = Assert.Single(clip.Tracks.Where(track => track.Path == "Spine"));
            Assert.Equal(2, spine.Keys.Count);

            // The root joint maps to the empty path, which is how a clip drives the entity itself.
            // Losing it would silently strip root motion from every import.
            Assert.Contains(clip.Tracks, track => track.Path.Length == 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ADanglingPathFailsWithAMessage()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"eidolon-missing-{Guid.NewGuid():N}.fbx");

        Assert.Throws<FileNotFoundException>(() => AssimpModelImporter.Import(missing));
    }

    [Fact]
    public void SpawnedModelsBecomeNamedEntitiesWithAClip()
    {
        var world = new World();

        var clip = new AnimationClip("Spin", 2f,
        [
            new AnimationTrack("Blade",
            [
                new PoseKey(0f, LocalPose.Identity),
                new PoseKey(2f, LocalPose.Identity with { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1f) }),
            ]),
        ]);

        var model = new ModelAsset
        {
            Name = "Widget",
            Nodes =
            [
                new ModelNode { Name = "Root", Children = [1] },
                new ModelNode { Name = "Blade", Position = new Vector3(0f, 1f, 0f), Mesh = PrimitiveMeshes.Cube },
            ],
            Clips = [clip],
        };

        var root = ModelSpawner.Spawn(world, model, new Material("Widget"), new Vector3(4f, 0f, 0f));

        Assert.Equal("Root", world.GetComponent<Name>(root).Value);
        Assert.Equal(new Vector3(4f, 0f, 0f), world.GetComponent<Transform>(root).Position);

        var blade = Hierarchy.GetChildren(world, root).Single();
        Assert.Equal("Blade", world.GetComponent<Name>(blade).Value);
        VectorAssert.Near(new Vector3(0f, 1f, 0f), world.GetComponent<Transform>(blade).Position);
        Assert.NotNull(world.GetComponent<MeshRenderer>(blade).Mesh);

        Assert.Same(clip, world.GetComponent<AnimationPlayer>(root).Clip);
    }

    [Fact]
    public void AnImportedFileSpawnsAsEntities()
    {
        // The whole editor path minus the file dialog: import, then instantiate into the world.
        File.WriteAllText(_path, TriangleObj);

        var world = new World();
        var root = ModelSpawner.Spawn(world, AssimpModelImporter.Import(_path), new Material("Imported"),
            new Vector3(0f, 2f, 0f));

        Assert.Equal(2, world.EntityCount);
        Assert.Equal(new Vector3(0f, 2f, 0f), world.GetComponent<Transform>(root).Position);

        var child = Hierarchy.GetChildren(world, root).Single();
        Assert.NotNull(world.GetComponent<MeshRenderer>(child).Mesh);
        Assert.False(string.IsNullOrWhiteSpace(world.GetComponent<Name>(child).Value));
    }
}
