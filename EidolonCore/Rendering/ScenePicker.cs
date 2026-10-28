using System.Numerics;
using EidolonCore.ECS;

namespace EidolonCore.Rendering;

/// <summary>Finds the entity under a world-space ray.</summary>
public static class ScenePicker
{
    /// <summary>
    /// Returns the closest entity whose mesh the ray hits, or null when it hits nothing.
    /// </summary>
    public static Entity? Pick(World world, in Ray ray) =>
        TryPick(world, ray, out var entity, out _, default) ? entity : null;

    /// <summary>
    /// Same as <see cref="Pick"/> but also reports the world-space hit point, which is what the
    /// editor uses to place a dropped asset on the surface under the cursor.
    /// </summary>
    /// <param name="ignore">Entity to skip, for example the drag preview itself.</param>
    public static bool TryPick(World world, in Ray ray, out Entity entity, out Vector3 point, Entity? ignore = null)
    {
        entity = default;
        point = default;
        var closest = float.MaxValue;

        foreach (var candidate in world.GetEntitiesWith<Transform, MeshRenderer>())
        {
            if (ignore is { } ignored && ignored == candidate)
                continue;

            var renderer = world.GetComponent<MeshRenderer>(candidate);
            if (renderer.Mesh is not { } mesh)
                continue;

            var worldMatrix = world.GetComponent<Transform>(candidate).GetWorldMatrix(world);
            if (!mesh.TryIntersect(ray, worldMatrix, out var distance))
                continue;

            if (distance >= closest)
                continue;

            closest = distance;
            entity = candidate;
        }

        if (!entity.IsValid)
            return false;

        point = ray.Origin + ray.Direction * closest;
        return true;
    }
}
