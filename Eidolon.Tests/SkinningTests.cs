using System.Numerics;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class SkinningTests
{
    private static Mesh SingleVertexMesh(Vector3 position, Vector3? normal = null) =>
        new("Bind", [new Vertex(position, normal ?? Vector3.UnitY)], [0, 0, 0]);

    private static SkinInfluence OneBone(int bone = 0, float weight = 1f)
    {
        var influence = SkinInfluence.None;
        influence.Add(bone, weight);
        return influence;
    }

    /// <summary>A world with a bone entity, and a skinned mesh bound to it at rest.</summary>
    private static (World World, Entity Bone, Entity MeshEntity, SkinnedMesh Skin) BindSingleBone(
        Vector3 vertex, Vector3 boneWorld = default)
    {
        var world = new World();

        var bone = world.CreateEntity();
        world.SetComponent(bone, new Transform(boneWorld));
        world.SetComponent(bone, new Name("Bone"));

        var meshEntity = world.CreateEntity();
        world.SetComponent(meshEntity, new Transform(Vector3.Zero));
        world.SetComponent(meshEntity, new Name("Body"));

        var bind = SingleVertexMesh(vertex);

        var skin = new SkinnedMesh
        {
            BindPose = bind,
            MeshEntity = meshEntity,
            BindWorld = Matrix4x4.Identity,
            Bones = [new SkeletonBone("Bone", Matrix4x4.Identity)],
            BoneEntities = [bone],
            Influences = [OneBone()],
            Vertices = new Vertex[1],
        };

        return (world, bone, meshEntity, skin);
    }

    [Fact]
    public void TheBindPoseDeformsToItself()
    {
        var (world, _, _, skin) = BindSingleBone(new Vector3(1f, 2f, 3f));

        Assert.Equal(1, skin.Deform(world));
        VectorAssert.Near(new Vector3(1f, 2f, 3f), skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void MovingABoneMovesItsWeightedVertices()
    {
        var (world, bone, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        var transform = world.GetComponent<Transform>(bone);
        transform.Position = new Vector3(0f, 1f, 0f);
        world.SetComponent(bone, transform);

        skin.Deform(world);

        VectorAssert.Near(new Vector3(1f, 1f, 0f), skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void ARotatedBoneTurnsItsVerticesAroundTheBoneInWorldSpace()
    {
        // The skinning matrix takes a vertex through the mesh node's bind world, back into the bone, and
        // out into the world again. Only a bind world that is not the identity proves the order of that
        // sandwich: with the wrong one the vertex still lands correctly at bind and flies off as soon as
        // the bone turns.
        var world = new World();

        var bone = world.CreateEntity();
        world.SetComponent(bone, new Transform(new Vector3(2f, 0f, 0f)));

        var meshEntity = world.CreateEntity();
        world.SetComponent(meshEntity, new Transform(new Vector3(2f, 0f, 0f)));

        var skin = new SkinnedMesh
        {
            BindPose = SingleVertexMesh(new Vector3(1f, 0f, 0f)),
            MeshEntity = meshEntity,
            BindWorld = Matrix4x4.CreateTranslation(2f, 0f, 0f),
            Bones = [new SkeletonBone("Bone", Matrix4x4.CreateTranslation(-2f, 0f, 0f))],
            BoneEntities = [bone],
            Influences = [OneBone()],
            Vertices = new Vertex[1],
        };

        var posed = new Transform(new Vector3(2f, 0f, 0f))
        {
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
        };

        world.SetComponent(bone, posed);

        var matrices = new Matrix4x4[1];
        Assert.Equal(1, skin.ComputeBoneMatrices(world, matrices));

        // The vertex sits one unit to the right of the bone, so a quarter turn lifts it straight up.
        var deformed = Vector3.Transform(new Vector3(1f, 0f, 0f), matrices[0]);
        var worldPosition = Vector3.Transform(deformed, world.GetComponent<Transform>(meshEntity).GetWorldMatrix(world));

        VectorAssert.Near(new Vector3(2f, 1f, 0f), worldPosition, 1e-4f);
    }

    [Fact]
    public void WeightsBlendBetweenTwoBones()
    {
        var world = new World();

        var bind = world.CreateEntity();
        world.SetComponent(bind, new Transform(Vector3.Zero));
        world.SetComponent(bind, new Name("Bind"));

        var moved = world.CreateEntity();
        world.SetComponent(moved, new Transform(new Vector3(0f, 2f, 0f)));
        world.SetComponent(moved, new Name("Moved"));

        var meshEntity = world.CreateEntity();
        world.SetComponent(meshEntity, new Transform(Vector3.Zero));

        var influence = SkinInfluence.None;
        influence.Add(0, 0.5f);
        influence.Add(1, 0.5f);

        var skin = new SkinnedMesh
        {
            BindPose = SingleVertexMesh(Vector3.Zero),
            MeshEntity = meshEntity,
            BindWorld = Matrix4x4.Identity,
            Bones = [new SkeletonBone("Bind", Matrix4x4.Identity), new SkeletonBone("Moved", Matrix4x4.Identity)],
            BoneEntities = [bind, moved],
            Influences = [influence],
            Vertices = new Vertex[1],
        };

        skin.Deform(world);

        // A quarter and a half of the way up, blended evenly.
        VectorAssert.Near(new Vector3(0f, 1f, 0f), skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void VerticesWithoutBonesStayInTheBindPose()
    {
        var world = new World();
        var bone = world.CreateEntity();
        world.SetComponent(bone, new Transform(new Vector3(5f, 0f, 0f)));

        var meshEntity = world.CreateEntity();
        world.SetComponent(meshEntity, new Transform(Vector3.Zero));

        var skin = new SkinnedMesh
        {
            BindPose = SingleVertexMesh(new Vector3(0f, 3f, 0f)),
            MeshEntity = meshEntity,
            BindWorld = Matrix4x4.Identity,
            Bones = [new SkeletonBone("Bone", Matrix4x4.Identity)],
            BoneEntities = [bone],
            Influences = [SkinInfluence.None],
            Vertices = new Vertex[1],
        };

        skin.Deform(world);

        VectorAssert.Near(new Vector3(0f, 3f, 0f), skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void DeformationStaysInTheMeshNodesLocalSpace()
    {
        // The whole model sits far from the origin; the deformed vertex must still come back relative to
        // the mesh node, because that is the space the renderer draws it in.
        var world = new World();

        var root = world.CreateEntity();
        world.SetComponent(root, new Transform(new Vector3(10f, 0f, 0f)));

        // Parented without preserving world transforms, so the local values stay the authored ones and
        // this test exercises the space conversion rather than reparenting.
        var bone = world.CreateEntity();
        world.SetComponent(bone, new Transform(Vector3.Zero));
        Hierarchy.TrySetParent(world, bone, root, preserveWorldTransform: false);

        var meshEntity = world.CreateEntity();
        world.SetComponent(meshEntity, new Transform(Vector3.Zero));
        Hierarchy.TrySetParent(world, meshEntity, root, preserveWorldTransform: false);

        var bindWorld = world.GetComponent<Transform>(meshEntity).GetWorldMatrix(world);
        var bindBoneWorld = world.GetComponent<Transform>(bone).GetWorldMatrix(world);

        var skin = new SkinnedMesh
        {
            BindPose = SingleVertexMesh(new Vector3(1f, 0f, 0f)),
            MeshEntity = meshEntity,
            BindWorld = bindWorld,
            Bones = [new SkeletonBone("Bone", Matrix4x4.Invert(bindBoneWorld, out var inverse) ? inverse : Matrix4x4.Identity)],
            BoneEntities = [bone],
            Influences = [OneBone()],
            Vertices = new Vertex[1],
        };

        skin.Deform(world);
        VectorAssert.Near(new Vector3(1f, 0f, 0f), skin.Vertices[0].Position, 1e-4f);

        var transform = world.GetComponent<Transform>(bone);
        transform.Position = new Vector3(0f, 1f, 0f);
        world.SetComponent(bone, transform);

        skin.Deform(world);
        VectorAssert.Near(new Vector3(1f, 1f, 0f), skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void NormalsStayUnitLengthUnderScale()
    {
        var (world, bone, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        var transform = world.GetComponent<Transform>(bone);
        transform.Scale = new Vector3(2f);
        world.SetComponent(bone, transform);

        skin.Deform(world);

        Assert.Equal(1f, skin.Vertices[0].Normal.Length(), 4);
    }

    [Fact]
    public void DeformingUpdatesTheRenderableMesh()
    {
        var (world, bone, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        skin.Deform(world);

        var renderable = skin.Deformed!;
        var version = renderable.Version;

        Assert.Equal(new Vector3(1f, 0f, 0f), renderable.Vertices[0].Position);

        var transform = world.GetComponent<Transform>(bone);
        transform.Position = new Vector3(0f, 1f, 0f);
        world.SetComponent(bone, transform);

        skin.Deform(world);

        Assert.True(renderable.Version > version, "the renderable mesh did not change, so nothing would re-upload");
        Assert.Equal(new Vector3(1f, 1f, 0f), renderable.Vertices[0].Position);
    }

    [Fact]
    public void TheSkinningSystemDeformsEverySkinnedMesh()
    {
        var (world, _, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        var entity = world.CreateEntity();
        world.SetComponent(entity, skin);

        var system = new EidolonCore.Nexus.Animation.SkinningSystem();
        system.Update(world, default);

        Assert.Equal(1, system.DeformedVertices);
        Assert.Equal(1, skin.DeformedCount);
    }

    [Fact]
    public void SpawnedSkinnedModelsResolveBonesAndRestOnTheirBindPose()
    {
        var world = new World();
        var bind = SingleVertexMesh(new Vector3(0f, 1f, 0f));

        var model = new ModelAsset
        {
            Nodes =
            [
                new ModelNode { Name = "Root", Children = [1, 2] },
                new ModelNode { Name = "Spine", Position = new Vector3(0f, 1f, 0f) },
                new ModelNode
                {
                    Name = "Body",
                    Mesh = bind,
                    Skin = new ModelSkin { BoneNames = ["Spine"], Influences = [OneBone()] },
                },
            ],
        };

        var root = ModelSpawner.Spawn(world, model, new Material("Body"), new Vector3(10f, 0f, 0f), scale: 2f);
        var body = Hierarchy.GetChildren(world, root).Single(child => world.GetComponent<Name>(child).Value == "Body");
        var skin = world.GetComponent<SkinnedMesh>(body);

        Assert.Single(skin.Bones);
        Assert.True(skin.BoneEntities[0].IsValid);
        Assert.Equal("Spine", world.GetComponent<Name>(skin.BoneEntities[0]).Value);

        // Placement and scale do not leak into the deformed geometry: the bind pose is skinned in the mesh
        // node's own space, so the matrix returns the vertex where the model put it locally.
        var matrices = new Matrix4x4[skin.Bones.Length];
        Assert.Equal(1, skin.ComputeBoneMatrices(world, matrices));

        VectorAssert.Near(new Vector3(0f, 1f, 0f),
            Vector3.Transform(skin.BindPose.Vertices[0].Position, matrices[0]), 1e-3f);
    }

    [Fact]
    public void GpuVerticesCarryTheBindPoseAndItsInfluences()
    {
        var (world, _, _, skin) = BindSingleBone(new Vector3(2f, 3f, 4f));

        var vertices = skin.BuildSkinnedVertices();

        Assert.Single(vertices);
        Assert.Equal(new Vector3(2f, 3f, 4f), vertices[0].Position);
        Assert.Equal(new Vector4(1f, 0f, 0f, 0f), vertices[0].BoneWeights);
        Assert.Equal(0f, vertices[0].BoneWeights.Y);
        Assert.Equal(0f, vertices[0].BoneIndices.X);
        Assert.Equal(new Vector3(2f, 3f, 4f), skin.BindPose.Vertices[0].Position);
    }

    [Fact]
    public void GpuBoneMatricesReproduceTheCpuDeformation()
    {
        // The two paths must agree exactly, or the same character would look different on GPU and CPU.
        var (world, bone, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        var transform = world.GetComponent<Transform>(bone);
        transform.Position = new Vector3(0f, 1f, 0f);
        world.SetComponent(bone, transform);

        var matrices = new Matrix4x4[skin.Bones.Length];
        Assert.Equal(skin.Bones.Length, skin.ComputeBoneMatrices(world, matrices));

        skin.Deform(world);

        var influence = skin.Influences[0];
        var fromMatrices = Vector3.Transform(skin.BindPose.Vertices[0].Position, matrices[influence.Bone0]);

        VectorAssert.Near(fromMatrices, skin.Vertices[0].Position, 1e-4f);
    }

    [Fact]
    public void ComputingBoneMatricesNeedsRoomForEveryBone()
    {
        var (world, _, _, skin) = BindSingleBone(new Vector3(1f, 0f, 0f));

        Assert.Equal(0, skin.ComputeBoneMatrices(world, Span<Matrix4x4>.Empty));
    }
}