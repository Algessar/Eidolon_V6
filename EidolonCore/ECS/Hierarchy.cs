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

        // A non-invertible parent (zero scale) leaves the local transform alone.
        transform.TrySetWorldMatrix(world, worldMatrix);
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

    /// <summary>
    /// Finds a descendant by slash-separated <see cref="Name"/> path, e.g. <c>"Rotor/Blade1"</c>.
    /// </summary>
    /// <remarks>
    /// An empty path means <paramref name="root"/> itself, which is how a clip animates the entity its
    /// player sits on. Segments are matched in order, so a name only has to be unique among its
    /// siblings; the first match wins when it is not.
    /// </remarks>
    /// <returns>False when any segment has no child with that name.</returns>
    public static bool TryFindByPath(World world, Entity root, string path, out Entity entity)
    {
        entity = root;

        if (string.IsNullOrWhiteSpace(path))
            return true;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (!TryFindChildByName(world, current, segment, out var child))
            {
                entity = default;
                return false;
            }

            current = child;
        }

        entity = current;
        return true;
    }

    /// <summary>The first child of <paramref name="parent"/> whose name matches, compared ordinally.</summary>
    public static bool TryFindChildByName(World world, Entity parent, string name, out Entity child)
    {
        foreach (var candidate in GetChildren(world, parent))
        {
            if (world.TryGetComponent<Name>(candidate, out var candidateName) &&
                string.Equals(candidateName.Value, name, StringComparison.Ordinal))
            {
                child = candidate;
                return true;
            }
        }

        child = default;
        return false;
    }
}
