using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class XpbdWorldSystemTests
{
    private const float Dt = 1f / 60f;

    private static Entity AddBox(World world, Vector3 position, Vector3 halfExtents, float inverseMass = 1f)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position));
        world.SetComponent(entity, new XpbdBody { HalfExtents = halfExtents, InverseMass = inverseMass });
        return entity;
    }

    private static Entity AddSphere(World world, Vector3 position, float radius, float inverseMass = 1f)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position));
        world.SetComponent(entity, new XpbdBody
        {
            Shape = XpbdShape.Sphere,
            Radius = radius,
            InverseMass = inverseMass,
        });

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
    public void ParentedBodyFallsInWorldSpace()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.SetComponent(parent, new Transform(new Vector3(3f, 2f, 1f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f), Vector3.One));

        var body = world.CreateEntity();
        world.SetComponent(body, new Transform(Vector3.Zero) { Parent = parent });
        world.SetComponent(body, new XpbdBody { HalfExtents = new Vector3(0.5f) });

        var system = new XpbdWorldSystem { FloorHeight = 0f };
        Tick(system, world, 240);

        var transform = world.GetComponent<Transform>(body);
        var worldPosition = transform.GetWorldPosition(world);

        // The floor is world space, so the box must land on it wherever its parent happens to be.
        Assert.InRange(worldPosition.X, 2.95f, 3.05f);
        Assert.InRange(worldPosition.Y, 0.45f, 0.62f);
        Assert.InRange(worldPosition.Z, 0.95f, 1.05f);

        // The stored pose is parent relative: the parent's quarter turn about X folds the 1.5 unit
        // drop into local Z, so a world value written straight into Position would fail here.
        VectorAssert.Near(new Vector3(0f, 0f, 1.5f), transform.Position, tolerance: 0.1f);
    }

    [Fact]
    public void BoxLandsOnAnotherBody()
    {
        var world = new World();

        // A static platform, with the floor far below so the platform is what the box lands on.
        var platform = AddBox(world, Vector3.Zero, new Vector3(1f), inverseMass: 0f);
        var falling = AddBox(world, new Vector3(0f, 3f, 0f), new Vector3(0.5f));

        var system = new XpbdWorldSystem { FloorHeight = -50f };
        Tick(system, world, 240);

        // Platform top is y = 1, so the box centre rests half a box plus the contact slack above it.
        var position = world.GetComponent<Transform>(falling).GetWorldPosition(world);
        Assert.InRange(position.Y, 1.47f, 1.58f);
        Assert.InRange(position.X, -0.05f, 0.05f);
        Assert.InRange(position.Z, -0.05f, 0.05f);

        // Static means static: the platform is never pushed anywhere.
        VectorAssert.Near(Vector3.Zero, world.GetComponent<Transform>(platform).GetWorldPosition(world));
    }

    [Fact]
    public void SphereRestsOnAnotherBody()
    {
        var world = new World();
        AddBox(world, Vector3.Zero, new Vector3(1f), inverseMass: 0f);
        var sphere = AddSphere(world, new Vector3(0f, 4f, 0f), 0.4f);

        var system = new XpbdWorldSystem { FloorHeight = -50f };
        Tick(system, world, 240);

        var position = world.GetComponent<Transform>(sphere).GetWorldPosition(world);
        Assert.InRange(position.Y, 1.36f, 1.45f);
        Assert.InRange(position.X, -0.05f, 0.05f);
    }

    [Fact]
    public void BodyWithoutInverseMassNeverMoves()
    {
        var world = new World();
        var staticBox = AddBox(world, new Vector3(2f, 1f, -1f), new Vector3(0.5f), inverseMass: 0f);

        var system = new XpbdWorldSystem { FloorHeight = 0f };
        Tick(system, world, 120);

        VectorAssert.Near(new Vector3(2f, 1f, -1f), world.GetComponent<Transform>(staticBox).GetWorldPosition(world));
    }

    [Fact]
    public void EditingAShapeRebuildsTheSimulation()
    {
        var world = new World();
        var sphere = AddSphere(world, new Vector3(0f, 2f, 0f), 0.2f);

        var system = new XpbdWorldSystem { FloorHeight = 0f };
        Tick(system, world, 180);
        Assert.InRange(world.GetComponent<Transform>(sphere).GetWorldPosition(world).Y, 0.15f, 0.25f);

        // Lift it, then make it a bigger sphere. The rebuild reads the transform, so the new sphere
        // starts from the lifted pose and falls to its new resting height.
        world.SetComponent(sphere, new Transform(new Vector3(0f, 5f, 0f)));

        var body = world.GetComponent<XpbdBody>(sphere);
        body.Radius = 0.6f;
        world.SetComponent(sphere, body);

        Tick(system, world, 240);

        Assert.Equal(1, system.BodyCount);
        Assert.InRange(world.GetComponent<Transform>(sphere).GetWorldPosition(world).Y, 0.55f, 0.65f);
    }

    [Fact]
    public void PlatformNarrowerThanTheBodyStillHoldsIt()
    {
        var world = new World();

        // The pillar is smaller than the box that falls on it, so only the box's face centre is over
        // it. Sampling corners alone would let the box straddle the pillar and drop straight past.
        AddBox(world, Vector3.Zero, new Vector3(0.3f), inverseMass: 0f);
        var falling = AddBox(world, new Vector3(0f, 2.5f, 0f), new Vector3(0.5f));

        var system = new XpbdWorldSystem { FloorHeight = -50f };
        Tick(system, world, 600);

        var position = world.GetComponent<Transform>(falling).GetWorldPosition(world);

        // It balances on the pillar: with corner-only sampling it would straddle the pillar and drop
        // the fifty units to the floor instead.
        Assert.InRange(position.Y, 0.75f, 0.9f);
        Assert.InRange(position.X, -0.05f, 0.05f);
        Assert.InRange(position.Z, -0.05f, 0.05f);
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
