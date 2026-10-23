using System.Numerics;

namespace Eidolon.Tests;

internal static class VectorAssert
{
    public static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-5f)
    {
        Assert.True(Vector3.Distance(expected, actual) <= tolerance,
            $"Expected {expected}, got {actual} (tolerance {tolerance}).");
    }
}
