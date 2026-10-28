using System.Numerics;

namespace Eidolon.Tests;

internal static class VectorAssert
{
    public static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-5f)
    {
        Assert.True(Vector3.Distance(expected, actual) <= tolerance,
            $"Expected {expected}, got {actual} (tolerance {tolerance}).");
    }

    /// <summary>Compares orientations, treating q and -q as the same rotation.</summary>
    public static void RotationNear(Quaternion expected, Quaternion actual, float tolerance = 1e-4f)
    {
        var dot = MathF.Abs(Quaternion.Dot(Quaternion.Normalize(expected), Quaternion.Normalize(actual)));
        var difference = 1f - dot;

        Assert.True(difference <= tolerance,
            $"Expected rotation {expected}, got {actual} (difference {difference}).");
    }

    public static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tolerance = 1e-4f)
    {
        for (var row = 0; row < 4; row++)
        {
            Assert.True(Vector4.Distance(expected[row], actual[row]) <= tolerance,
                $"Row {row}: expected {expected[row]}, got {actual[row]} (tolerance {tolerance}).");
        }
    }
}
