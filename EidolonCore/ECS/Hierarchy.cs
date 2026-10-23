using System.Numerics;

namespace EidolonCore.ECS;

/// <summary>Parent/child relationships between transforms.</summary>
public static class Hierarchy
{
    /// <summary>
    /// Parents <paramref name="child"/> under <paramref name="parent"/>, or makes it a root when
    /// <paramref name="parent"/> is invalid.
    /// </summary>
    /// <param name="preserveWorldTransform">
    /// When true (the default) the child's local transform is recomputed so it stays exactly where
    /// it is in the world. Reparenting is otherwise a jump scare: the local transform is unchanged,
    /// so the entity teleports by the new parent's transform.
    /// </param>
    /// <returns>False when the change is impossible or would create a cycle.</returns>
    public static bool TrySetParent(World world, Entity child, Entity parent, bool preserveWorldTransform = true)
    {
        if (!world.IsAlive(child) || !world.HasComponent<Transform>(child))
            return false;

        if (parent.IsValid)
        {
            if (parent == child || !world.IsAlive(parent) || !world.HasComponent<Transform>(parent))
                return false;

            // Reparenting a node under its own descendant would form a cycle.
            if (IsAncestorOf(world, ancestor: child, entity: parent))
                return false;
        }

        Matrix4x4 worldMatrix = default;

        if (preserveWorldTransform)
            worldMatrix = world.GetComponent<Transform>(child).GetWorldMatrix(world);

        ref var transform = ref world.GetComponent<Transform>(child);
        transform.Parent = parent;

        if (!preserveWorldTransform)
            return true;

        var parentMatrix = Matrix4x4.Identity;

        if (parent.IsValid && world.TryGetComponent<Transform>(parent, out var parentTransform))
            parentMatrix = parentTransform.GetWorldMatrix(world);

        if (!Matrix4x4.Invert(parentMatrix, out var inverseParent))
            return true;

        // A non-invertible parent (zero scale) leaves the local transform alone.
        if (!Matrix4x4.Decompose(worldMatrix * inverseParent, out var scale, out var rotation, out var translation))
            return true;

        transform.Scale = scale;
        transform.Rotation = rotation;
        transform.Position = translation;
        return true;
    }

    /// <summary>True when <paramref name="ancestor"/> is <paramref name="entity"/> or one of its ancestors.</summary>
    public static bool IsAncestorOf(World world, Entity ancestor, Entity entity)
    {
        var current = entity;

        for (var depth = 0; current.IsValid && depth <= Transform.MaxHierarchyDepth; depth++)
        {
            if (current == ancestor)
                return true;

            if (!world.TryGetComponent<Transform>(current, out var transform))
                return false;

            current = transform.Parent;
        }

        return false;
    }

    /// <summary>The entities whose transform is parented directly to <paramref name="parent"/>.</summary>
    public static IEnumerable<Entity> GetChildren(World world, Entity parent)
    {
        foreach (var entity in world.GetEntitiesWith<Transform>())
        {
            if (world.GetComponent<Transform>(entity).Parent == parent)
                yield return entity;
        }
    }

    /// <summary>Entities with no parent, i.e. the roots of the hierarchy.</summary>
    public static IEnumerable<Entity> GetRoots(World world)
    {
        foreach (var entity in world.GetEntitiesWith<Transform>())
        {
            if (!world.GetComponent<Transform>(entity).Parent.IsValid)
                yield return entity;
        }
    }
}
