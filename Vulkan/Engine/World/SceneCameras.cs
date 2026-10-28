using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

/// <summary>
/// Keeps camera components in step with the entity transform that owns them.
/// </summary>
/// <remarks>
/// A camera entity has both a <see cref="Transform"/> and a <see cref="Camera"/>. The transform is
/// what the editor's gizmo and gameplay move, so the camera follows it: moving preserves the look
/// direction, while rotating it turns the view.
/// </remarks>
public static class SceneCameras
{
    /// <summary>Pushes every camera entity's transform into its camera component.</summary>
    public static void SyncFromTransforms(World world)
    {
        foreach (var entity in world.GetEntitiesWith<Transform, Camera>())
        {
            var transform = world.GetComponent<Transform>(entity);
            var position = transform.GetWorldPosition(world);
            var rotation = transform.GetWorldRotation(world);

            ref var camera = ref world.GetComponent<Camera>(entity);

            var distance = Vector3.Distance(camera.Target, camera.Position);
            if (distance < 0.1f)
                distance = 1f;

            var positionDelta = position - camera.Position;
            var rotated = MathF.Abs(Quaternion.Dot(rotation, camera.TransformRotation)) < 1f - 1e-4f;

            if (rotated)
            {
                // The transform was rotated: turn the view instead of re-aiming it.
                camera.Position = position;
                camera.Target = position + CameraOrientation.ForwardFrom(rotation) * distance;
            }
            else if (positionDelta.LengthSquared() > 1e-12f)
            {
                camera.Position = position;
                camera.Target += positionDelta;
            }

            camera.TransformRotation = rotation;
        }
    }
}
