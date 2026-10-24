using System.Numerics;
using EidolonCore.Nexus.Sdf;
using EidolonCore.Nexus.Xpbd;

namespace Eidolon.Tests;

public class SdfContactTests
{
    private const float Dt = 1f / 60f;

    private static (XpbdSolver Solver, int Particle) BallOverPlane(float radius, float startHeight)
    {
        var solver = new XpbdSolver { Iterations = 8 };
        var particle = solver.AddParticle(new Vector3(0f, startHeight, 0f));

        solver.AddConstraint(new SdfContactConstraint(
            new SdfPlane(Vector3.Zero, Vector3.UnitY), radius, particle));

        return (solver, particle);
    }

    [Fact]
    public void FallingBallRestsOnTheSurface()
    {
        const float radius = 0.25f;
        var (solver, index) = BallOverPlane(radius, 2f);

        for (var step = 0; step < 180; step++)
            solver.Step(Dt);

        var y = solver.Particles[index].Position.Y;

        // It should settle on top of the plane, not sink through it.
        Assert.InRange(y, radius - 0.02f, radius + 0.02f);
    }

    [Fact]
    public void DeepPenetrationIsPushedOutInOneStep()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var index = solver.AddParticle(new Vector3(0f, -0.5f, 0f));

        solver.AddConstraint(new SdfContactConstraint(
            new SdfPlane(Vector3.Zero, Vector3.UnitY), 0.1f, index));

        solver.Step(Dt);

        Assert.True(solver.Particles[index].Position.Y >= 0.1f - 1e-3f,
            $"expected the particle to be pushed above the surface, was {solver.Particles[index].Position.Y}");
    }

    [Fact]
    public void ContactReportsWhichParticlesAreTouching()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var above = solver.AddParticle(new Vector3(0f, 1f, 0f));
        var inside = solver.AddParticle(new Vector3(0f, 0.05f, 0f));

        var contact = new SdfContactConstraint(new SdfPlane(Vector3.Zero, Vector3.UnitY), 0.2f, above, inside);
        solver.AddConstraint(contact);

        solver.Step(Dt);

        Assert.Single(contact.ActiveContacts);
        Assert.Equal(1, contact.ActiveContacts[0]);
    }

    [Fact]
    public void ComposedSurfacesActAsOneFloor()
    {
        // A plane plus a box will be well outside the particle, so the union of the two behaves
        // like the plane: the point is that a consumer only holds one ISdf.
        ISdf floor = new SdfUnion(
            new SdfPlane(Vector3.Zero, Vector3.UnitY),
            new SdfBox(new Vector3(10f, 0f, 0f), new Vector3(1f)));

        var solver = new XpbdSolver { Iterations = 8 };
        var index = solver.AddParticle(new Vector3(0f, 1.5f, 0f));
        solver.AddConstraint(new SdfContactConstraint(floor, 0.25f, index));

        for (var step = 0; step < 180; step++)
            solver.Step(Dt);

        Assert.InRange(solver.Particles[index].Position.Y, 0.23f, 0.27f);
    }

    [Fact]
    public void PinnedParticlesIgnoreContacts()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var pinned = solver.AddParticle(new Vector3(0f, -1f, 0f), inverseMass: 0f);

        solver.AddConstraint(new SdfContactConstraint(
            new SdfPlane(Vector3.Zero, Vector3.UnitY), 0.5f, pinned));

        solver.Step(Dt);

        VectorAssert.Near(new Vector3(0f, -1f, 0f), solver.Particles[pinned].Position);
    }
}
