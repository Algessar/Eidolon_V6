using System.Numerics;
using EidolonCore.Nexus.Sdf;

namespace Eidolon.Tests;

public class SdfTests
{
    private const float Epsilon = 1e-3f;

    private static readonly ISdf Sphere = new SdfSphere(Vector3.Zero, 1f);
    private static readonly ISdf Plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
    private static readonly ISdf Box = new SdfBox(Vector3.Zero, new Vector3(1f));
    private static readonly ISdf Capsule = new SdfCapsule(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f), 0.5f);

    public static TheoryData<string, ISdf> AllFields => new()
    {
        { "sphere", Sphere },
        { "plane", Plane },
        { "box", Box },
        { "capsule", Capsule },
    };

    [Fact]
    public void SphereDistanceIsSigned()
    {
        Assert.Equal(1f, Sphere.Distance(new Vector3(2f, 0f, 0f)), 3);
        Assert.Equal(0f, Sphere.Distance(new Vector3(1f, 0f, 0f)), 3);
        Assert.Equal(-1f, Sphere.Distance(Vector3.Zero), 3);

        VectorAssert.Near(new Vector3(1f, 0f, 0f), Sphere.Normal(new Vector3(2f, 0f, 0f)));
    }

    [Fact]
    public void PlaneIsAHalfSpace()
    {
        Assert.Equal(3f, Plane.Distance(new Vector3(0f, 3f, 0f)), 3);
        Assert.Equal(-2f, Plane.Distance(new Vector3(0f, -2f, 0f)), 3);
        VectorAssert.Near(Vector3.UnitY, Plane.Normal(new Vector3(5f, -2f, 7f)));
    }

    [Fact]
    public void BoxDistanceHandlesFacesEdgesAndInterior()
    {
        Assert.Equal(1f, Box.Distance(new Vector3(2f, 0f, 0f)), 3);
        Assert.Equal(MathF.Sqrt(2f), Box.Distance(new Vector3(2f, 2f, 0f)), 3);
        Assert.Equal(-1f, Box.Distance(Vector3.Zero), 3);
        Assert.Equal(-0.1f, Box.Distance(new Vector3(0.9f, 0f, 0f)), 3);

        VectorAssert.Near(Vector3.UnitX, Box.Normal(new Vector3(2f, 0f, 0f)));
        VectorAssert.Near(Vector3.UnitX, Box.Normal(new Vector3(0.9f, 0f, 0f)));
    }

    [Fact]
    public void CapsuleUsesTheSegmentAndRadius()
    {
        Assert.Equal(0.5f, Capsule.Distance(new Vector3(1f, 0f, 0f)), 3);
        Assert.Equal(0.5f, Capsule.Distance(new Vector3(1f, 1f, 0f)), 3);
        Assert.Equal(-0.5f, Capsule.Distance(Vector3.Zero), 3);

        VectorAssert.Near(Vector3.UnitX, Capsule.Normal(new Vector3(1f, 0.5f, 0f)));
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    public void AnalyticNormalMatchesTheDistanceGradient(string name, ISdf field)
    {
        // The analytic normal is only defined off the surface, so probe exterior points.
        Vector3[] probes =
        [
            new(2f, 0.5f, 0.25f),
            new(-1.5f, 2f, 0.5f),
            new(0.75f, 1.25f, 2f),
        ];

        foreach (var probe in probes)
        {
            if (field.Distance(probe) < 0.2f)
                continue;

            var sampled = new Vector3(
                field.Distance(probe + new Vector3(Epsilon, 0f, 0f)) - field.Distance(probe - new Vector3(Epsilon, 0f, 0f)),
                field.Distance(probe + new Vector3(0f, Epsilon, 0f)) - field.Distance(probe - new Vector3(0f, Epsilon, 0f)),
                field.Distance(probe + new Vector3(0f, 0f, Epsilon)) - field.Distance(probe - new Vector3(0f, 0f, Epsilon)));

            var expected = Vector3.Normalize(sampled);
            var actual = field.Normal(probe);

            Assert.Equal(1f, actual.Length(), 3);
            VectorAssert.Near(expected, actual, 0.02f);
        }
    }

    [Fact]
    public void SampleReportsSideAndNormalTogether()
    {
        var sample = Sphere.Sample(new Vector3(2f, 0f, 0f));

        Assert.Equal(1f, sample.Distance, 3);
        Assert.False(sample.IsInside);
        Assert.True(Sphere.Sample(Vector3.Zero).IsInside);
    }
}
