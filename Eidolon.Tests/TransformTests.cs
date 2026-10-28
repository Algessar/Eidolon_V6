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
        Assert.Equal(Matrix4x4.Identity, transform.GetLocalMatrix());
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

    [Fact]
    public void WorldPositionComposesTheParentChain()
    {
        var world = new World();
        var parent = Add(world, new Vector3(10f, 0f, 0f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f));
        var child = Add(world, new Vector3(0f, 0f, 1f), parent: parent);

        // The parent's rotation takes the child's +Z offset to +X.
        VectorAssert.Near(new Vector3(11f, 0f, 0f), world.GetComponent<Transform>(child).GetWorldPosition(world));
    }

    [Fact]
    public void WorldRotationComposesLocalThenParent()
    {
        var world = new World();
        var parentRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f);
        var childRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.2f);
        var parent = Add(world, Vector3.Zero, parentRotation);
        var child = Add(world, Vector3.Zero, childRotation, parent: parent);

        // The child's local rotation is applied first, then the parent's.
        var expected = Vector3.Transform(
            Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateFromQuaternion(childRotation)),
            Matrix4x4.CreateFromQuaternion(parentRotation));

        var actual = world.GetComponent<Transform>(child).GetWorldRotation(world);
        VectorAssert.Near(expected, Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateFromQuaternion(actual)),
            tolerance: 1e-4f);
    }

    [Fact]
    public void SetWorldPositionStoresAParentRelativePosition()
    {
        var world = new World();
        var parentRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);
        var parent = Add(world, new Vector3(5f, 1f, -2f), parentRotation, new Vector3(2f, 2f, 2f));
        var child = Add(world, Vector3.Zero, parent: parent);

        var target = new Vector3(1f, 4f, 3f);

        ref var transform = ref world.GetComponent<Transform>(child);
        Assert.True(transform.TrySetWorldPosition(world, target));

        VectorAssert.Near(target, world.GetComponent<Transform>(child).GetWorldPosition(world));
        VectorAssert.Near(new Vector3(5f, 1f, -2f), world.GetComponent<Transform>(parent).GetWorldPosition(world));
    }

    [Fact]
    public void SetWorldRotationStoresAParentRelativeRotation()
    {
        var world = new World();
        var parent = Add(world, new Vector3(1f, 2f, 3f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f));
        var child = Add(world, Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f), parent: parent);

        var target = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1.1f);

        ref var transform = ref world.GetComponent<Transform>(child);
        Assert.True(transform.TrySetWorldRotation(world, target));

        VectorAssert.RotationNear(target, world.GetComponent<Transform>(child).GetWorldRotation(world));
    }

    [Fact]
    public void SetWorldPoseWritesBothUnderAParent()
    {
        var world = new World();
        var parent = Add(world, new Vector3(-1f, 2f, 4f), Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f),
            new Vector3(2f, 2f, 2f));
        var child = Add(world, Vector3.Zero, parent: parent);

        var position = new Vector3(2f, -3f, 0.5f);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 2.2f);

        ref var transform = ref world.GetComponent<Transform>(child);
        Assert.True(transform.TrySetWorldPose(world, position, rotation));

        var solved = world.GetComponent<Transform>(child);
        VectorAssert.Near(position, solved.GetWorldPosition(world));
        VectorAssert.RotationNear(rotation, solved.GetWorldRotation(world));
    }

    [Fact]
    public void SetWorldMatrixRoundTripsThroughAParent()
    {
        var world = new World();
        var parent = Add(world, new Vector3(2f, -1f, 0.5f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.6f));
        var child = Add(world, Vector3.Zero, parent: parent);

        var target = Matrix4x4.CreateScale(new Vector3(2f, 3f, 4f)) *
                     Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f)) *
                     Matrix4x4.CreateTranslation(new Vector3(4f, -2f, 1f));

        ref var transform = ref world.GetComponent<Transform>(child);
        Assert.True(transform.TrySetWorldMatrix(world, target));

        VectorAssert.MatrixNear(target, world.GetComponent<Transform>(child).GetWorldMatrix(world), tolerance: 1e-3f);
    }

    [Fact]
    public void CollapsedParentLeavesTheLocalTransformAlone()
    {
        var world = new World();
        var parent = Add(world, Vector3.Zero, scale: Vector3.Zero);
        var child = Add(world, new Vector3(0.25f, 0.5f, 0.75f), parent: parent);

        var original = world.GetComponent<Transform>(child);
        ref var transform = ref world.GetComponent<Transform>(child);

        Assert.False(transform.TrySetWorldPosition(world, new Vector3(9f, 9f, 9f)));
        Assert.False(transform.TrySetWorldRotation(world, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1f)));
        Assert.False(transform.TrySetWorldPose(world, new Vector3(9f, 9f, 9f), Quaternion.Identity));
        Assert.False(transform.TrySetWorldMatrix(world, Matrix4x4.CreateTranslation(9f, 9f, 9f)));

        Assert.Equal(original.Position, world.GetComponent<Transform>(child).Position);
        Assert.Equal(original.Rotation, world.GetComponent<Transform>(child).Rotation);
    }

    [Fact]
    public void RootTransformsWriteTheirWorldValueDirectly()
    {
        var world = new World();
        var entity = Add(world, new Vector3(1f, 2f, 3f));

        ref var transform = ref world.GetComponent<Transform>(entity);
        Assert.True(transform.TrySetWorldPosition(world, new Vector3(-4f, 5f, 6f)));

        Assert.Equal(new Vector3(-4f, 5f, 6f), world.GetComponent<Transform>(entity).Position);
    }

    private static Entity Add(World world, Vector3 position, Quaternion? rotation = null, Vector3? scale = null,
        Entity parent = default)
    {
        var entity = world.CreateEntity();

        world.SetComponent(entity, new Transform(position, rotation ?? Quaternion.Identity, scale ?? Vector3.One)
        {
            Parent = parent,
        });

        return entity;
    }
}
