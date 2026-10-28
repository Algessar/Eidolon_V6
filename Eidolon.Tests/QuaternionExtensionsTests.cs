using System.Numerics;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class QuaternionExtensionsTests
{
    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0.6f, 0.3f, 0.2f)]
    [InlineData(-1.2f, 0.9f, -0.4f)]
    [InlineData(2.0f, -0.6f, 1.1f)]
    public void RoundTripsThroughQuaternion(float x, float y, float z)
    {
        var quaternion = QuaternionExtensions.FromEulerAngles(x, y, z);

        var (actualX, actualY, actualZ) = quaternion.ToEulerAngles();

        Assert.Equal(x, actualX, 4);
        Assert.Equal(y, actualY, 4);
        Assert.Equal(z, actualZ, 4);
    }

    [Fact]
    public void IdentityRotationIsZeroAngles()
    {
        var (x, y, z) = Quaternion.Identity.ToEulerAngles();

        Assert.Equal(0f, x, 4);
        Assert.Equal(0f, y, 4);
        Assert.Equal(0f, z, 4);
    }

    [Fact]
    public void EachAngleIsARightHandedTurnAboutItsOwnAxis()
    {
        var quarter = MathF.PI / 2f;

        // +X takes +Y to +Z, +Y takes +Z to +X, +Z takes +X to +Y.
        VectorAssert.Near(Vector3.UnitZ, Rotate(QuaternionExtensions.FromEulerAngles(quarter, 0f, 0f), Vector3.UnitY));
        VectorAssert.Near(Vector3.UnitX, Rotate(QuaternionExtensions.FromEulerAngles(0f, quarter, 0f), Vector3.UnitZ));
        VectorAssert.Near(Vector3.UnitY, Rotate(QuaternionExtensions.FromEulerAngles(0f, 0f, quarter), Vector3.UnitX));
    }

    [Fact]
    public void AnglesAreAppliedInXyzOrder()
    {
        // A quarter turn about Y followed by the same about X is not the same as the reverse, so the
        // order has to be observable: this pins X first, then Y, then Z.
        var quarter = MathF.PI / 2f;

        var fromQuaternion = Rotate(QuaternionExtensions.FromEulerAngles(quarter, quarter, 0f), Vector3.UnitZ);

        var xThenY = Vector3.Transform(
            Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationX(quarter)),
            Matrix4x4.CreateRotationY(quarter));

        VectorAssert.Near(xThenY, fromQuaternion);
    }

    [Fact]
    public void GimbalLockStillRebuildsTheSameRotation()
    {
        var quaternion = QuaternionExtensions.FromEulerAngles(0.4f, MathF.PI / 2f, 0.9f);

        var (x, y, z) = quaternion.ToEulerAngles();

        Assert.Equal(MathF.PI / 2f, y, 4);

        // The individual X and Z angles are not recoverable, but the orientation must survive.
        VectorAssert.RotationNear(quaternion, QuaternionExtensions.FromEulerAngles(x, y, z));
    }

    private static Vector3 Rotate(Quaternion rotation, Vector3 direction) =>
        Vector3.Transform(direction, Matrix4x4.CreateFromQuaternion(rotation));
}
