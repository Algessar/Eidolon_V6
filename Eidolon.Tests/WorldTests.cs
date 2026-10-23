using System.Numerics;
using EidolonCore.ECS;

namespace Eidolon.Tests;

public class WorldTests
{
    private sealed class Marker : IComponent
    {
        public int Value;
    }

    private static World WorldWithEntity(out Entity entity)
    {
        var world = new World();
        entity = world.CreateEntity();
        return world;
    }

    [Fact]
    public void CreateEntityAssignsUniqueIds()
    {
        var world = new World();

        var first = world.CreateEntity();
        var second = world.CreateEntity();

        Assert.NotEqual(first, second);
        Assert.True(first.IsValid);
        Assert.Equal(2, world.EntityCount);
        Assert.True(world.IsAlive(first));
    }

    [Fact]
    public void DefaultEntityIsNotValid()
    {
        Assert.False(default(Entity).IsValid);
    }

    [Fact]
    public void SetAndGetComponent()
    {
        var world = WorldWithEntity(out var entity);

        world.SetComponent(entity, new Transform(new Vector3(1, 2, 3)));

        Assert.True(world.HasComponent<Transform>(entity));
        Assert.Equal(new Vector3(1, 2, 3), world.GetComponent<Transform>(entity).Position);
    }

    [Fact]
    public void GetComponentReturnsWritableReference()
    {
        var world = WorldWithEntity(out var entity);
        world.SetComponent(entity, Transform.Identity);

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Position = new Vector3(7, 8, 9);

        Assert.Equal(new Vector3(7, 8, 9), world.GetComponent<Transform>(entity).Position);
    }

    [Fact]
    public void SetComponentReplacesExistingValue()
    {
        var world = WorldWithEntity(out var entity);

        world.SetComponent(entity, new Marker { Value = 1 });
        world.SetComponent(entity, new Marker { Value = 2 });

        Assert.Equal(2, world.GetComponent<Marker>(entity).Value);
        Assert.Single(world.GetEntitiesWith<Marker>());
    }

    [Fact]
    public void GetComponentThrowsWhenMissing()
    {
        var world = WorldWithEntity(out var entity);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = world.GetComponent<Transform>(entity);
        });
    }

    [Fact]
    public void TryGetComponentReportsMissingWithoutThrowing()
    {
        var world = WorldWithEntity(out var entity);

        Assert.False(world.TryGetComponent<Transform>(entity, out _));
        Assert.True(world.HasComponent<Transform>(entity) is false);
    }

    [Fact]
    public void RemoveComponent()
    {
        var world = WorldWithEntity(out var entity);
        world.SetComponent(entity, Transform.Identity);

        Assert.True(world.RemoveComponent<Transform>(entity));
        Assert.False(world.HasComponent<Transform>(entity));
        Assert.False(world.RemoveComponent<Transform>(entity));
    }

    [Fact]
    public void SetComponentOnUnknownEntityThrows()
    {
        var world = new World();

        Assert.Throws<InvalidOperationException>(() =>
            world.SetComponent(new Entity(999), Transform.Identity));
    }

    [Fact]
    public void DestroyEntityRemovesComponents()
    {
        var world = WorldWithEntity(out var entity);
        world.SetComponent(entity, Transform.Identity);
        world.SetComponent(entity, new Marker());

        Assert.True(world.DestroyEntity(entity));

        Assert.False(world.IsAlive(entity));
        Assert.Equal(0, world.EntityCount);
        Assert.Empty(world.GetEntitiesWith<Transform>());
        Assert.Empty(world.GetEntitiesWith<Marker>());
        Assert.False(world.DestroyEntity(entity));
    }

    [Fact]
    public void GetEntitiesWithTwoTypesFiltersToEntitiesHavingBoth()
    {
        var world = new World();

        var both = world.CreateEntity();
        world.SetComponent(both, Transform.Identity);
        world.SetComponent(both, new Marker());

        var transformOnly = world.CreateEntity();
        world.SetComponent(transformOnly, Transform.Identity);

        var markerOnly = world.CreateEntity();
        world.SetComponent(markerOnly, new Marker());

        var matches = world.GetEntitiesWith<Transform, Marker>().ToArray();

        Assert.Equal([both], matches);
    }

    [Fact]
    public void GetEntitiesWithMissingComponentTypeIsEmpty()
    {
        var world = new World();
        world.CreateEntity();

        Assert.Empty(world.GetEntitiesWith<Transform>());
    }
}
