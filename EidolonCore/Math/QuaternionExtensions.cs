using System.Numerics;

namespace EidolonCore.Math;

/// <summary>Conversion between quaternions and yaw/pitch/roll (YXZ) Euler angles.</summary>
public static class QuaternionExtensions
{
    /// <summary>
    /// Decomposes a rotation into yaw, pitch and roll in radians. This is the inverse of
    /// <see cref="Quaternion.CreateFromYawPitchRoll"/>.
    /// </summary>
    public static (float Yaw, float Pitch, float Roll) ToYawPitchRoll(this Quaternion rotation)
    {
        var x = rotation.X;
        var y = rotation.Y;
        var z = rotation.Z;
        var w = rotation.W;

        // Elements of the equivalent column-vector rotation matrix. Row/column indices are
        // spelled out because the sign errors here are very easy to make.
        var row1Col2 = 2f * (y * z - x * w);
        var row1Col0 = 2f * (x * y + z * w);
        var row1Col1 = 1f - 2f * (x * x + z * z);
        var row0Col2 = 2f * (x * z + y * w);
        var row2Col2 = 1f - 2f * (x * x + y * y);

        var pitch = MathF.Asin(Mathf.Clamp(-row1Col2, -1f, 1f));
        var roll = MathF.Atan2(row1Col0, row1Col1);
        var yaw = MathF.Atan2(row0Col2, row2Col2);

        return (yaw, pitch, roll);
    }
}
