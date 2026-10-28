using System.Numerics;
using EidolonCore.Nexus.Sdf;

namespace Eidolon.Tests;

public class SdfTransformedTests
{
    [Fact]
    public void DistanceMatchesTheLocalFieldThroughThePose()
    {
        var field = new SdfTransformed(new SdfSphere(Vector3.Zero, 1f));
        field.SetPose(new Vector3(5f, 0f, 0f), Quaternion.Identity);

        Assert.Equal(0f, field.Distance(new Vector3(6f, 0f, 0f)), 5);
        Assert.Equal(2f, field.Distance(new Vector3(8f, 0f, 0f)), 5);
    }

    [Fact]
    public void NormalIsRotatedIntoWorldSpace()
    {
        var field = new SdfTransformed(new SdfBox(Vector3.Zero, new Vector3(1f)));
        field.SetPose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f));

        // The local -X face of a quarter-turned box faces world +Z, so that is where it pushes from.
        Assert.Equal(0.2f, field.Distance(new Vector3(0f, 0f, 1.2f)), 5);
        VectorAssert.Near(Vector3.UnitZ, field.Normal(new Vector3(0f, 0f, 1.2f)), 1e-4f);
    }

    [Fact]
    public void MovingThePoseMovesTheSurface()
    {
        var field = new SdfTransformed(new SdfSphere(Vector3.Zero, 0.5f));
        field.SetPose(Vector3.Zero, Quaternion.Identity);

        Assert.True(field.Distance(new Vector3(1f, 0f, 0f)) > 0f);

        field.SetPose(new Vector3(1f, 0f, 0f), Quaternion.Identity);

        Assert.Equal(-0.5f, field.Distance(new Vector3(1f, 0f, 0f)), 5);
    }
}
