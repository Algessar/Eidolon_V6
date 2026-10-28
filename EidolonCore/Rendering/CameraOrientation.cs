using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>Conversion between a camera's look direction and the rotation of the transform that owns it.</summary>
/// <remarks>
/// A camera entity keeps its orientation on the transform, where the hierarchy, the gizmo and gameplay
/// all expect it, and derives its look direction from that. These two functions are the only place the
/// two representations meet, so a spawner, the editor and the camera sync cannot drift apart.
/// </remarks>
public static class CameraOrientation
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
}
