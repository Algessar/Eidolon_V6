using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class XpbdWorldSystemTests
{
    private const float Dt = 1f / 60f;

    private static Entity AddBox(World world, Vector3 position, Vector3 halfExtents)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position));
        world.SetComponent(entity, new XpbdBody { HalfExtents = halfExtents });
        return entity;
    }

    private static void Tick(ISystem system, World world, int steps)
    {
        for (var step = 0; step < steps; step++)
            system.Update(world, new FrameTime(Dt, step * Dt, step));
    }

    [Fact]
    public void BoxFallsAndRestsOnTheFloor()
    {
        var world = new World();
        var entity = AddBox(world, new Vector3(0f, 3f, 0f), new Vector3(0.5f));
        var system = new XpbdWorldSystem { FloorHeight = 0f };

        Tick(system, world, 240);

        // Half a box above the plane, with the contact radius as slack.
        Assert.InRange(world.GetComponent<Transform>(entity).Position.Y, 0.45f, 0.62f);
        Assert.Equal(1, system.BodyCount);
    }

    [Fact]
    public void EntitiesWithoutABodyAreLeftAlone()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(0f, 5f, 0f)));

        Tick(new XpbdWorldSystem(), world, 30);

        VectorAssert.Near(new Vector3(0f, 5f, 0f), world.GetComponent<Transform>(entity).Position);
    }

    [Fact]
    public void AddingABodyRebuildsTheSimulation()
    {
        var world = new World();
        var system = new XpbdWorldSystem { FloorHeight = 0f };

        AddBox(world, new Vector3(0f, 2f, 0f), new Vector3(0.5f));
        Tick(system, world, 2);
        Assert.Equal(1, system.BodyCount);

        AddBox(world, new Vector3(2f, 2f, 0f), new Vector3(0.25f));
        Tick(system, world, 2);
        Assert.Equal(2, system.BodyCount);
    }

    [Fact]
    public void RotationIsWrittenBackToTheTransform()
    {
        var world = new World();

        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(new Vector3(0f, 2f, 0f), Quaternion.Identity, Vector3.One));
        world.SetComponent(entity, new XpbdBody { HalfExtents = new Vector3(0.5f) });

        // Tilt it, then let it drop onto one edge: the solved rotation must reach the transform.
        var system = new XpbdWorldSystem { FloorHeight = 0f };
        Tick(system, world, 120);

        var rotation = world.GetComponent<Transform>(entity).Rotation;
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.Normalize(rotation), Quaternion.Identity)) > 0.5f,
            $"unexpected rotation {rotation}");
    }

    [Fact]
    public void NewWorldRebuildsInsteadOfReusingTheOldSimulation()
    {
        var system = new XpbdWorldSystem { FloorHeight = 0f };

        // First play session: the box falls all the way to the floor.
        var first = new World();
        var firstBox = AddBox(first, new Vector3(0f, 3f, 0f), new Vector3(0.5f));
        Tick(system, first, 240);
        Assert.InRange(first.GetComponent<Transform>(firstBox).Position.Y, 0.45f, 0.62f);

        // Second session: a new world with identical entity ids, exactly like the play-mode clone.
        var second = new World();
        var secondBox = AddBox(second, new Vector3(0f, 4f, 0f), new Vector3(0.5f));
        Assert.Equal(firstBox.Id, secondBox.Id);

        system.Update(second, new FrameTime(Dt, 0f, 0));

        // One step of free fall, not the final resting position of the previous session.
        Assert.InRange(second.GetComponent<Transform>(secondBox).Position.Y, 3.98f, 4.01f);
    }
}
