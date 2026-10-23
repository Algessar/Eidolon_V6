using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Vulkan;

/// <summary>
/// Keeps camera components in step with the entity transform that owns them.
/// </summary>
/// <remarks>
/// A camera entity has both a <see cref="Transform"/> and a <see cref="Camera"/>. The transform is
/// what the editor's gizmo moves, so the camera has to follow it; the look direction is preserved by
/// moving the target along with the position, otherwise moving a camera would swing the view back
/// towards wherever it used to be aimed.
/// </remarks>
public static class SceneCameras
{
    /// <summary>Pushes every camera entity's world position into its camera component.</summary>
    public static void SyncFromTransforms(World world)
    {
        foreach (var entity in world.GetEntitiesWith<Transform, Camera>())
        {
            var matrix = world.GetComponent<Transform>(entity).GetWorldMatrix(world);
            var position = new Vector3(matrix.M41, matrix.M42, matrix.M43);

            ref var camera = ref world.GetComponent<Camera>(entity);
            var delta = position - camera.Position;

            if (delta.LengthSquared() < 1e-12f)
                continue;

            camera.Target += delta;
            camera.Position = position;
        }
    }
}
