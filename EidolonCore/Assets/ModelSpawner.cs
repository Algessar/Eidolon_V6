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

        return entities[0];
    }
}
