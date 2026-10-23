using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Tests;

public class WorldCloneTests
{
    private sealed class MutableComponent : IComponent
    {
        public int Value;
    }

    [Fact]
    public void CloneCopiesEntitiesAndKeepsTheirIds()
    {
        var world = new World();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.SetComponent(first, new Transform(new Vector3(1, 2, 3)));
        world.SetComponent(second, new Transform(new Vector3(4, 5, 6)));

        var clone = world.Clone();

        Assert.Equal(world.EntityCount, clone.EntityCount);
        Assert.True(clone.IsAlive(first));
        Assert.True(clone.IsAlive(second));
        VectorAssert.Near(new Vector3(1, 2, 3), clone.GetComponent<Transform>(first).Position);
        VectorAssert.Near(new Vector3(4, 5, 6), clone.GetComponent<Transform>(second).Position);
    }

    [Fact]
    public void ClonedValueComponentsAreIndependent()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(1, 0, 0)));

        var clone = world.Clone();
        clone.GetComponent<Transform>(entity).Position = new Vector3(9, 9, 9);

        VectorAssert.Near(new Vector3(1, 0, 0), world.GetComponent<Transform>(entity).Position);
    }

    [Fact]
    public void ComponentsWithoutACopierAreShared()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var shared = new MutableComponent { Value = 1 };
        world.SetComponent(entity, shared);

        var clone = world.Clone();

        Assert.Same(shared, clone.GetComponent<MutableComponent>(entity));
    }

    [Fact]
    public void CopiersProduceIndependentInstances()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, new MutableComponent { Value = 1 });

        var clone = world.Clone(new Dictionary<Type, Func<IComponent, IComponent>>
        {
            [typeof(MutableComponent)] = component => new MutableComponent { Value = ((MutableComponent)component).Value },
        });

        var copied = clone.GetComponent<MutableComponent>(entity);
        Assert.NotSame(world.GetComponent<MutableComponent>(entity), copied);
        Assert.Equal(1, copied.Value);

        copied.Value = 42;
        Assert.Equal(1, world.GetComponent<MutableComponent>(entity).Value);
    }

    [Fact]
    public void EntitiesCreatedAfterCloningDoNotReuseIds()
    {
        var world = new World();
        var existing = world.CreateEntity();
        world.SetComponent(existing, Transform.Identity);

        var clone = world.Clone();
        var added = clone.CreateEntity();

        Assert.NotEqual(existing, added);
        Assert.False(world.IsAlive(added));
    }

    [Fact]
    public void CloneOfAnEmptyWorldIsEmpty()
    {
        var clone = new World().Clone();

        Assert.Equal(0, clone.EntityCount);
    }

    [Fact]
    public void ParentsStayValidInTheClone()
    {
        var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(10, 0, 0)));
        world.SetComponent(child, new Transform(new Vector3(1, 0, 0)));
        Assert.True(Hierarchy.TrySetParent(world, child, parent));

        var clone = world.Clone();

        Assert.Equal(parent, clone.GetComponent<Transform>(child).Parent);
        VectorAssert.Near(Vector3.Transform(Vector3.Zero, world.GetComponent<Transform>(child).GetWorldMatrix(world)),
            Vector3.Transform(Vector3.Zero, clone.GetComponent<Transform>(child).GetWorldMatrix(clone)));
    }
}
