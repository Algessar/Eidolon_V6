namespace EidolonCore.ECS;

/// <summary>Parent/child relationships between transforms.</summary>
public static class Hierarchy
{
    /// <summary>
    /// Parents <paramref name="child"/> under <paramref name="parent"/>, or makes it a root when
    /// <paramref name="parent"/> is invalid.
    /// </summary>
    /// <returns>False when the change is impossible or would create a cycle.</returns>
    public static bool TrySetParent(World world, Entity child, Entity parent)
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

        ref var transform = ref world.GetComponent<Transform>(child);
        transform.Parent = parent;
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
