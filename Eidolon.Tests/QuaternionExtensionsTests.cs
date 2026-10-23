using System.Numerics;
using EidolonCore.Math;

namespace Eidolon.Tests;

public class QuaternionExtensionsTests
{
    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0.6f, 0.3f, 0.2f)]
    [InlineData(-1.2f, 0.9f, -0.4f)]
    [InlineData(2.0f, -0.6f, 1.1f)]
    public void RoundTripsThroughQuaternion(float yaw, float pitch, float roll)
    {
        var quaternion = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);

        var (actualYaw, actualPitch, actualRoll) = quaternion.ToYawPitchRoll();

        Assert.Equal(yaw, actualYaw, 4);
        Assert.Equal(pitch, actualPitch, 4);
        Assert.Equal(roll, actualRoll, 4);
    }

    [Fact]
    public void IdentityRotationIsZeroAngles()
    {
        var (yaw, pitch, roll) = Quaternion.Identity.ToYawPitchRoll();

        Assert.Equal(0f, yaw, 4);
        Assert.Equal(0f, pitch, 4);
        Assert.Equal(0f, roll, 4);
    }

    [Fact]
    public void YawOnlyRotationIsReportedAsYaw()
    {
        var quaternion = Quaternion.CreateFromYawPitchRoll(1.0f, 0f, 0f);

        var (yaw, pitch, roll) = quaternion.ToYawPitchRoll();

        Assert.Equal(1.0f, yaw, 4);
        Assert.Equal(0f, pitch, 4);
        Assert.Equal(0f, roll, 4);
    }
}
