using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

/// <summary>
/// Creates scene entities. Shared by the default scene and the editor's Browser so both build
/// drawable objects the same way.
/// </summary>
public static class SceneSpawner
{
    public static Entity SpawnMesh(World world, Mesh mesh, Material material, Vector3 position, Vector3 scale,
        Quaternion? rotation = null)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position, rotation ?? Quaternion.Identity, scale));
        world.SetComponent(entity, new MeshRenderer(mesh, material));
        return entity;
    }

    /// <summary>
    /// Spawns a mesh scaled to fit a unit box, with its bounds centre at, <paramref name="center"/>.
    /// </summary>
    /// <remarks>
    /// Imported models come in whatever units they were authored in, so this is what makes an
    /// arbitrary OBJ appear at a predictable size when it is dragged into the scene. Note that
    /// the entity's <see cref="Transform.Position"/> is the origin-adjusted position, not
    /// <paramref name="center"/>; pivots should really be baked at import time.
    /// </remarks>
    public static Entity SpawnFittedMesh(World world, Mesh mesh, Material material, Vector3 center,
        Quaternion? rotation = null)
    {
        var bounds = mesh.LocalBounds;
        var maxDimension = bounds.MaxDimension;

        var scale = maxDimension > 1e-6f ? 1f / maxDimension : 1f;
        var origin = center - bounds.Center * scale;

        return SpawnMesh(world, mesh, material, origin, new Vector3(scale), rotation);
    }
}
