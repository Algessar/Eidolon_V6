using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;

namespace EidolonCore.Assets;

/// <summary>Instantiates an imported model into the world.</summary>
/// <remarks>
/// Nodes become named entities in the transform hierarchy, so imported geometry can be selected, moved,
/// parented, given physics bodies and animated by path like anything authored in the editor. A model with
/// clips gets an <see cref="AnimationPlayer"/> on its root holding the first one.
/// </remarks>
public static class ModelSpawner
{
    /// <summary>Spawns the model and returns its root entity.</summary>
    /// <param name="scale">Uniform scale applied to the root, useful for models authored in centimetres.</param>
    public static Entity Spawn(World world, ModelAsset model, Material material, Vector3 position,
        Quaternion? rotation = null, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(model);

        if (model.Nodes.Count == 0)
            throw new ArgumentException("The model has no nodes.", nameof(model));

        var entities = new Entity[model.Nodes.Count];

        for (var index = 0; index < model.Nodes.Count; index++)
        {
            var node = model.Nodes[index];

            entities[index] = world.CreateEntity();
            world.SetComponent(entities[index],
                new Transform(node.Position, node.Rotation, node.Scale));
            world.SetComponent(entities[index], new Name(node.Name));

            if (node.Mesh is { } mesh)
                world.SetComponent(entities[index], new MeshRenderer(mesh, material));
        }

        for (var index = 0; index < model.Nodes.Count; index++)
        {
            foreach (var child in model.Nodes[index].Children)
            {
                // Both sides already hold local transforms, so the hierarchy is restored as authored.
                Hierarchy.TrySetParent(world, entities[child], entities[index], preserveWorldTransform: false);
            }
        }

        ref var root = ref world.GetComponent<Transform>(entities[0]);
        root.Position = position;
        root.Rotation = rotation ?? Quaternion.Identity;
        root.Scale = new Vector3(scale);

        if (model.Clips.Count > 0)
        {
            world.SetComponent(entities[0], new AnimationPlayer
            {
                Clip = model.Clips[0],
                Loop = true,
            });
        }

        // Skins are bound last, so the bones' bind-pose world matrices already include the placement
        // above: wherever the model is put, the bind pose is where its geometry is.
        for (var index = 0; index < model.Nodes.Count; index++)
        {
            var node = model.Nodes[index];

            if (node.Skin is not { } skin || node.Mesh is not { } bindPose)
                continue;

            AttachSkin(world, entities[index], entities[0], bindPose, skin);
        }

        return entities[0];
    }

    private static void AttachSkin(World world, Entity entity, Entity root, Mesh bindPose, ModelSkin skin)
    {
        var bones = new SkeletonBone[skin.BoneNames.Count];
        var boneEntities = new Entity[skin.BoneNames.Count];

        for (var index = 0; index < skin.BoneNames.Count; index++)
        {
            var bone = TryFindByName(world, root, skin.BoneNames[index]);
            boneEntities[index] = bone;

            var inverseBind = Matrix4x4.Identity;

            if (bone.IsValid &&
                Matrix4x4.Invert(world.GetComponent<Transform>(bone).GetWorldMatrix(world), out var inverse))
            {
                inverseBind = inverse;
            }

            bones[index] = new SkeletonBone(skin.BoneNames[index], inverseBind);
        }

        // The mesh keeps its bind pose: the renderer deforms it in the vertex shader, so the CPU never
        // writes a second copy of the vertices.
        world.SetComponent(entity, new SkinnedMesh
        {
            BindPose = bindPose,
            MeshEntity = entity,
            BindWorld = world.GetComponent<Transform>(entity).GetWorldMatrix(world),
            Bones = bones,
            BoneEntities = boneEntities,
            Influences = skin.Influences.ToArray(),
        });
    }

    /// <summary>First entity in the model subtree with that name, or an invalid entity.</summary>
    private static Entity TryFindByName(World world, Entity root, string name)
    {
        if (world.TryGetComponent<Name>(root, out var rootName) &&
            string.Equals(rootName.Value, name, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (var child in Hierarchy.GetChildren(world, root))
        {
            var found = TryFindByName(world, child, name);

            if (found.IsValid)
                return found;
        }

        return default;
    }
}
