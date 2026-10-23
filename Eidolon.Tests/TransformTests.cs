using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Tests;

public class TransformTests
{
    [Fact]
    public void NewTransformIsIdentity()
    {
        var transform = new Transform();

        Assert.Equal(Vector3.Zero, transform.Position);
        Assert.Equal(Quaternion.Identity, transform.Rotation);
        Assert.Equal(Vector3.One, transform.Scale);
        Assert.Equal(System.Numerics.Matrix4x4.Identity, transform.GetLocalMatrix());
    }

    [Fact]
    public void WorldMatrixTranslatesOrigin()
    {
        var transform = new Transform(new Vector3(1, 2, 3));

        VectorAssert.Near(new Vector3(1, 2, 3), Vector3.Transform(Vector3.Zero, transform.GetLocalMatrix()));
    }

    [Fact]
    public void WorldMatrixAppliesScaleBeforeTranslation()
    {
        var transform = new Transform(new Vector3(10, 0, 0), Quaternion.Identity, new Vector3(2, 2, 2));

        VectorAssert.Near(new Vector3(12, 0, 0), Vector3.Transform(new Vector3(1, 0, 0), transform.GetLocalMatrix()));
    }

    [Fact]
    public void WorldMatrixAppliesRotationAboutY()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        var transform = new Transform(Vector3.Zero, rotation, Vector3.One);

        // Right-handed rotation about +Y takes +X towards -Z.
        VectorAssert.Near(new Vector3(0, 0, -1), Vector3.Transform(Vector3.UnitX, transform.GetLocalMatrix()));
    }
}
