using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Tests;

public class HierarchyTests
{
    private static (World World, Entity Parent, Entity Child) ParentAndChild()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(10, 0, 0)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(1, 0, 0)));

        Assert.True(Hierarchy.TrySetParent(world, child, parent));
        return (world, parent, child);
    }

    private static Vector3 WorldPosition(World world, Entity entity) =>
        Vector3.Transform(Vector3.Zero, world.GetComponent<Transform>(entity).GetWorldMatrix(world));

    [Fact]
    public void RootWorldMatrixEqualsLocalMatrix()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(1, 2, 3)));

        VectorAssert.Near(new Vector3(1, 2, 3), WorldPosition(world, entity));
    }

    [Fact]
    public void ParentingKeepsTheChildInPlace()
    {
        var (world, _, child) = ParentAndChild();

        // The child was authored at (1,0,0) and must stay there despite the parent at (10,0,0).
        VectorAssert.Near(new Vector3(1, 0, 0), WorldPosition(world, child));
        VectorAssert.Near(new Vector3(-9, 0, 0), world.GetComponent<Transform>(child).Position);
    }

    [Fact]
    public void ParentingWithoutPreservingWorldUsesTheLocalTransform()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(10, 0, 0)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(1, 0, 0)));

        Assert.True(Hierarchy.TrySetParent(world, child, parent, preserveWorldTransform: false));

        VectorAssert.Near(new Vector3(1, 0, 0), world.GetComponent<Transform>(child).Position);
        VectorAssert.Near(new Vector3(11, 0, 0), WorldPosition(world, child));
    }

    [Fact]
    public void NestedParentsComposeInOrder()
    {
        var world = new World();

        var a = world.CreateEntity();
        world.SetComponent(a, new Transform(new Vector3(1, 0, 0)));

        var b = world.CreateEntity();
        world.SetComponent(b, new Transform(new Vector3(2, 0, 0)));

        var c = world.CreateEntity();
        world.SetComponent(c, new Transform(new Vector3(4, 0, 0)));

        Assert.True(Hierarchy.TrySetParent(world, b, a));
        Assert.True(Hierarchy.TrySetParent(world, c, b));

        // Everything kept its authored world position ...
        VectorAssert.Near(new Vector3(1, 0, 0), WorldPosition(world, a));
        VectorAssert.Near(new Vector3(2, 0, 0), WorldPosition(world, b));
        VectorAssert.Near(new Vector3(4, 0, 0), WorldPosition(world, c));

        // ... and the locals now describe the offsets between them.
        VectorAssert.Near(new Vector3(1, 0, 0), world.GetComponent<Transform>(b).Position);
        VectorAssert.Near(new Vector3(2, 0, 0), world.GetComponent<Transform>(c).Position);
    }

    [Fact]
    public void UnparentingKeepsTheChildInPlace()
    {
        var (world, _, child) = ParentAndChild();

        Assert.True(Hierarchy.TrySetParent(world, child, default));

        Assert.False(world.GetComponent<Transform>(child).Parent.IsValid);
        VectorAssert.Near(new Vector3(1, 0, 0), WorldPosition(world, child));
        Assert.Contains(child, Hierarchy.GetRoots(world));
    }

    [Fact]
    public void ReparentingBetweenParentsKeepsTheChildInPlace()
    {
        var world = new World();

        var first = world.CreateEntity();
        world.SetComponent(first, new Transform(new Vector3(5, 0, 0), Quaternion.Identity, new Vector3(2)));

        var second = world.CreateEntity();
        world.SetComponent(second, new Transform(new Vector3(-3, 1, 0)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(1, 0, 0)));

        Assert.True(Hierarchy.TrySetParent(world, child, first));
        var afterFirst = WorldPosition(world, child);

        Assert.True(Hierarchy.TrySetParent(world, child, second));
        VectorAssert.Near(afterFirst, WorldPosition(world, child));
    }

    [Fact]
    public void ParentingPreservesRotationAndScale()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(-4, 2, 1),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.1f), new Vector3(2, 2, 2)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(3, 1, -2),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.7f), new Vector3(0.5f)));

        var before = world.GetComponent<Transform>(child).GetWorldMatrix(world);

        Assert.True(Hierarchy.TrySetParent(world, child, parent));

        var after = world.GetComponent<Transform>(child).GetWorldMatrix(world);

        VectorAssert.Near(Vector3.Transform(Vector3.Zero, before), Vector3.Transform(Vector3.Zero, after));
        VectorAssert.Near(Vector3.Transform(Vector3.UnitX, before), Vector3.Transform(Vector3.UnitX, after));
        VectorAssert.Near(Vector3.Transform(Vector3.UnitY, before), Vector3.Transform(Vector3.UnitY, after));
    }

    [Fact]
    public void RejectsParentingAnEntityToItself()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, Transform.Identity);

        Assert.False(Hierarchy.TrySetParent(world, entity, entity));
    }

    [Fact]
    public void RejectsCycles()
    {
        var (world, parent, child) = ParentAndChild();

        // parent already owns child, so making child the parent of parent would loop.
        Assert.False(Hierarchy.TrySetParent(world, parent, child));
        Assert.False(world.GetComponent<Transform>(parent).Parent.IsValid);
    }

    [Fact]
    public void RootsAndChildrenReflectParenting()
    {
        var (world, parent, child) = ParentAndChild();

        Assert.Equal([parent], Hierarchy.GetRoots(world).ToArray());
        Assert.Equal([child], Hierarchy.GetChildren(world, parent).ToArray());
    }

    [Fact]
    public void DestroyingAParentOrphansItsChildrenWithoutMovingThem()
    {
        var (world, parent, child) = ParentAndChild();

        Assert.True(world.DestroyEntity(parent));

        Assert.False(world.GetComponent<Transform>(child).Parent.IsValid);
        VectorAssert.Near(new Vector3(1, 0, 0), WorldPosition(world, child));
    }

    [Fact]
    public void DestroySubtreeTakesTheWholeBranchWithIt()
    {
        var world = new World();

        var root = world.CreateEntity();
        world.SetComponent(root, new Transform(Vector3.Zero));

        var branch = world.CreateEntity();
        world.SetComponent(branch, new Transform(new Vector3(1f, 0f, 0f)));

        var leaf = world.CreateEntity();
        world.SetComponent(leaf, new Transform(new Vector3(0f, 1f, 0f)));

        var grandchild = world.CreateEntity();
        world.SetComponent(grandchild, new Transform(new Vector3(0f, 2f, 0f)));

        var bystander = world.CreateEntity();
        world.SetComponent(bystander, new Transform(new Vector3(9f, 0f, 0f)));

        Assert.True(Hierarchy.TrySetParent(world, branch, root));
        Assert.True(Hierarchy.TrySetParent(world, leaf, branch));
        Assert.True(Hierarchy.TrySetParent(world, grandchild, branch));

        // Editor deletion is a branch operation: a model or a prop is one thing.
        Assert.Equal(3, Hierarchy.DestroySubtree(world, branch));

        Assert.False(world.IsAlive(branch));
        Assert.False(world.IsAlive(leaf));
        Assert.False(world.IsAlive(grandchild));
        Assert.True(world.IsAlive(root));
        Assert.True(world.IsAlive(bystander));
    }

    [Fact]
    public void DestroySubtreeOnAMissingEntityDoesNothing()
    {
        var world = new World();

        Assert.Equal(0, Hierarchy.DestroySubtree(world, new Entity(404)));
    }

    [Fact]
    public void DestroyingAGrandparentKeepsTheWholeChainInPlace()
    {
        var world = new World();

        var grandparent = world.CreateEntity();
        world.SetComponent(grandparent, new Transform(new Vector3(10, 0, 0)));

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(2, 0, 0)));

        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(0, 3, 0)));

        Assert.True(Hierarchy.TrySetParent(world, parent, grandparent));
        Assert.True(Hierarchy.TrySetParent(world, child, parent));

        var parentWorld = WorldPosition(world, parent);
        var childWorld = WorldPosition(world, child);

        Assert.True(world.DestroyEntity(grandparent));

        VectorAssert.Near(parentWorld, WorldPosition(world, parent));
        VectorAssert.Near(childWorld, WorldPosition(world, child));
    }

    [Fact]
    public void BrokenParentChainDoesNotHang()
    {
        var world = new World();
        var child = world.CreateEntity();
        world.SetComponent(child, new Transform(new Vector3(5, 0, 0), Quaternion.Identity, Vector3.One));

        // Point at an entity that does not exist; the walk must stop and fall back to local.
        ref var transform = ref world.GetComponent<Transform>(child);
        transform.Parent = new Entity(999);

        VectorAssert.Near(new Vector3(5, 0, 0), WorldPosition(world, child));
    }
}
