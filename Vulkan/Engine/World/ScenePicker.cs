using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

/// <summary>Finds the entity under a world-space ray.</summary>
public static class ScenePicker
{
    /// <summary>
    /// Returns the closest entity whose mesh the ray hits, or null when it hits nothing.
    /// </summary>
    public static Entity? Pick(World world, in Ray ray)
    {
        Entity? picked = null;
        var closest = float.MaxValue;

        foreach (var entity in world.GetEntitiesWith<Transform, MeshRenderer>())
        {
            var renderer = world.GetComponent<MeshRenderer>(entity);
            if (renderer.Mesh is not { } mesh)
                continue;

            var worldMatrix = world.GetComponent<Transform>(entity).GetWorldMatrix(world);
            if (!mesh.TryIntersect(ray, worldMatrix, out var distance))
                continue;

            if (distance >= closest)
                continue;

            closest = distance;
            picked = entity;
        }

        return picked;
    }
}
