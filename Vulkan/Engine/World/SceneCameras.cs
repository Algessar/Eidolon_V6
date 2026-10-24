using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Vulkan;

/// <summary>
/// Keeps camera components in step with the entity transform that owns them.
/// </summary>
/// <remarks>
/// A camera entity has both a <see cref="Transform"/> and a <see cref="Camera"/>. The transform is
/// what the editor's gizmo moves and rotates, so the camera follows it: moving preserves the look
/// direction, while rotating it turns the view.
/// </remarks>
public static class SceneCameras
{
    /// <summary>World-space direction the camera looks in, from the entity's rotation.</summary>
    public static Vector3 ForwardFrom(Quaternion rotation)
    {
        var matrix = Matrix4x4.CreateFromQuaternion(rotation);
        var backward = new Vector3(matrix.M31, matrix.M32, matrix.M33);

        return backward.LengthSquared() < 1e-12f ? -Vector3.UnitZ : -Vector3.Normalize(backward);
    }

    /// <summary>Entity rotation that looks along <paramref name="forward"/>.</summary>
    public static Quaternion RotationFrom(Vector3 forward)
    {
        if (forward.LengthSquared() < 1e-12f)
            return Quaternion.Identity;

        var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Normalize(forward), Vector3.UnitY);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix4x4.Transpose(view)));
    }

    /// <summary>Pushes every camera entity's transform into its camera component.</summary>
    public static void SyncFromTransforms(World world)
    {
        foreach (var entity in world.GetEntitiesWith<Transform, Camera>())
        {
            var matrix = world.GetComponent<Transform>(entity).GetWorldMatrix(world);
            var position = new Vector3(matrix.M41, matrix.M42, matrix.M43);

            var rotation = Matrix4x4.Decompose(matrix, out _, out var decomposed, out _)
                ? decomposed
                : Quaternion.Identity;

            ref var camera = ref world.GetComponent<Camera>(entity);

            var distance = Vector3.Distance(camera.Target, camera.Position);
            if (distance < 0.1f)
                distance = 1f;

            var positionDelta = position - camera.Position;
            var rotated = MathF.Abs(Quaternion.Dot(rotation, camera.TransformRotation)) < 1f - 1e-4f;

            if (rotated)
            {
                // The gizmo rotated the entity: turn the view instead of re-aiming it.
                camera.Position = position;
                camera.Target = position + ForwardFrom(rotation) * distance;
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