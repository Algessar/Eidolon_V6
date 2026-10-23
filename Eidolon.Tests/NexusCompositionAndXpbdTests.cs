using System.Numerics;
using EidolonCore.Nexus.Sdf;
using EidolonCore.Nexus.Xpbd;

namespace Eidolon.Tests;

public class NexusCompositionAndXpbdTests
{
    [Fact]
    public void UnionKeepsTheNearestSurface()
    {
        ISdf union = new SdfUnion(new SdfSphere(new Vector3(-2f, 0f, 0f), 1f), new SdfSphere(new Vector3(2f, 0f, 0f), 1f));

        // A point above the left sphere is closest to it.
        VectorAssert.Near(new Vector3(0f, 1f, 0f), union.Normal(new Vector3(-2f, 2f, 0f)));
        Assert.Equal(1f, union.Distance(new Vector3(-2f, 2f, 0f)), 3);

        // Midway between the two surfaces is outside both.
        Assert.True(union.Distance(Vector3.Zero) > 0f);
    }

    [Fact]
    public void IntersectionIsInsideOnlyWhereBothAre()
    {
        ISdf intersection = new SdfIntersection(
            new SdfSphere(Vector3.Zero, 2f),
            new SdfPlane(new Vector3(0f, 0f, 0f), Vector3.UnitY));

        // The plane's inside is below it, so the intersection is the lower half of the sphere.
        Assert.True(intersection.Distance(new Vector3(0f, -1f, 0f)) < 0f);
        Assert.True(intersection.Distance(new Vector3(0f, 1f, 0f)) > 0f);
    }

    [Fact]
    public void SubtractionCarvesOneFieldOutOfAnother()
    {
        ISdf carved = new SdfSubtraction(new SdfSphere(Vector3.Zero, 2f), new SdfSphere(Vector3.Zero, 1f));

        // The shell is solid ...
        Assert.True(carved.Distance(new Vector3(1.5f, 0f, 0f)) < 0f);
        // ... the carved core is not ...
        Assert.True(carved.Distance(Vector3.Zero) > 0f);
        // ... and the outer boundary is still outside.
        Assert.True(carved.Distance(new Vector3(3f, 0f, 0f)) > 0f);
    }

    [Fact]
    public void FreeFallingParticleFollowsGravity()
    {
        var solver = new XpbdSolver();
        var index = solver.AddParticle(Vector3.Zero);

        const float dt = 1f / 60f;

        for (var step = 0; step < 60; step++)
            solver.Step(dt);

        // Semi-implicit Euler over one second is a little past 0.5 g t^2.
        var y = solver.Particles[index].Position.Y;
        Assert.InRange(y, -5.1f, -4.85f);
    }

    [Fact]
    public void PinnedParticlesDoNotMove()
    {
        var solver = new XpbdSolver();
        var pinned = solver.AddParticle(new Vector3(0f, 5f, 0f), inverseMass: 0f);
        var free = solver.AddParticle(new Vector3(0f, 4f, 0f));

        for (var step = 0; step < 30; step++)
            solver.Step(1f / 60f);

        VectorAssert.Near(new Vector3(0f, 5f, 0f), solver.Particles[pinned].Position);
        Assert.True(solver.Particles[free].Position.Y < 4f);
    }

    [Fact]
    public void DistanceConstraintRestoresTheRestLength()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero, Iterations = 8 };
        var anchor = solver.AddParticle(Vector3.Zero, inverseMass: 0f);
        var hanging = solver.AddParticle(new Vector3(0f, -3f, 0f));

        solver.AddConstraint(new DistanceConstraint(anchor, hanging, restLength: 1f));

        for (var step = 0; step < 10; step++)
            solver.Step(1f / 60f);

        var length = Vector3.Distance(solver.Particles[anchor].Position, solver.Particles[hanging].Position);
        Assert.Equal(1f, length, 2);
    }

    [Fact]
    public void ComplianceMakesTheConstraintSofter()
    {
        static float SettledLength(float compliance)
        {
            var solver = new XpbdSolver { Gravity = new Vector3(0f, -9.81f, 0f), Iterations = 8 };
            var anchor = solver.AddParticle(Vector3.Zero, inverseMass: 0f);
            var hanging = solver.AddParticle(new Vector3(0f, -1f, 0f));

            solver.AddConstraint(new DistanceConstraint(anchor, hanging, 1f, compliance));

            for (var step = 0; step < 60; step++)
                solver.Step(1f / 60f);

            return Vector3.Distance(solver.Particles[anchor].Position, solver.Particles[hanging].Position);
        }

        var stiff = SettledLength(0f);
        var soft = SettledLength(0.01f);

        Assert.True(soft > stiff, $"expected the soft constraint to stretch further ({soft} vs {stiff})");
    }
}
