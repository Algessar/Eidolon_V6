using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>Conversion between quaternions and XYZ Euler angles.</summary>
/// <remarks>
/// Euler angles exist for authoring, not for simulation: they are the form an inspector can show as
/// three numbers and animation curves can interpolate, while the engine keeps quaternions. The order
/// is XYZ, matching a point rotated about X, then Y, then Z, which is what Blender and Unity's
/// inspector present by default.
/// </remarks>
public static class QuaternionExtensions
{
    /// <summary>
    /// Decomposes a rotation into X, Y and Z angles in radians.
    /// </summary>
    /// <remarks>
    /// At gimbal lock (Y at ±90 degrees) the X and Z axes are driven along the same axis, so the two
    /// angles cannot be recovered separately: X is reported as zero and Z carries the whole twist.
    /// Rebuilding from the reported angles still reproduces the original rotation.
    /// </remarks>
    public static (float X, float Y, float Z) ToEulerAngles(this Quaternion rotation)
    {
        var matrix = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));

        // A quarter turn about Y collapses the X and Z rows onto each other, so the usual atan2
        // pairs lose their independent meaning. Detect that and fall back to a single twist.
        var locked = MathF.Abs(matrix.M13) >= 0.99999f;

        var y = MathF.Asin(System.Math.Clamp(-matrix.M13, -1f, 1f));
        var x = locked ? 0f : MathF.Atan2(matrix.M23, matrix.M33);
        var z = locked ? MathF.Atan2(-matrix.M21, matrix.M22) : MathF.Atan2(matrix.M12, matrix.M11);

        return (x, y, z);
    }

    /// <summary>Builds a rotation from X, Y and Z angles in radians, applied in that order.</summary>
    public static Quaternion FromEulerAngles(float x, float y, float z) =>
        Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(x) *
            Matrix4x4.CreateRotationY(y) *
            Matrix4x4.CreateRotationZ(z)));
}
