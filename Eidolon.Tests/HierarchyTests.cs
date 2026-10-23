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

    [Fact]
    public void RootWorldMatrixEqualsLocalMatrix()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(1, 2, 3)));

        var worldMatrix = world.GetComponent<Transform>(entity).GetWorldMatrix(world);

        VectorAssert.Near(new Vector3(1, 2, 3), Vector3.Transform(Vector3.Zero, worldMatrix));
    }

    [Fact]
    public void ChildWorldMatrixIncludesParent()
    {
        var (world, _, child) = ParentAndChild();

        var worldMatrix = world.GetComponent<Transform>(child).GetWorldMatrix(world);

        VectorAssert.Near(new Vector3(11, 0, 0), Vector3.Transform(Vector3.Zero, worldMatrix));
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

        var worldMatrix = world.GetComponent<Transform>(c).GetWorldMatrix(world);

        VectorAssert.Near(new Vector3(7, 0, 0), Vector3.Transform(Vector3.Zero, worldMatrix));
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
    public void DestroyingAParentOrphansItsChildren()
    {
        var (world, parent, child) = ParentAndChild();

        Assert.True(world.DestroyEntity(parent));

        Assert.False(world.GetComponent<Transform>(child).Parent.IsValid);
        VectorAssert.Near(new Vector3(1, 0, 0),
            Vector3.Transform(Vector3.Zero, world.GetComponent<Transform>(child).GetWorldMatrix(world)));
    }

    [Fact]
    public void ReparentingToInvalidMakesARoot()
    {
        var (world, _, child) = ParentAndChild();

        Assert.True(Hierarchy.TrySetParent(world, child, default));

        Assert.False(world.GetComponent<Transform>(child).Parent.IsValid);
        Assert.Contains(child, Hierarchy.GetRoots(world));
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

        VectorAssert.Near(new Vector3(5, 0, 0),
            Vector3.Transform(Vector3.Zero, world.GetComponent<Transform>(child).GetWorldMatrix(world)));
    }
}
