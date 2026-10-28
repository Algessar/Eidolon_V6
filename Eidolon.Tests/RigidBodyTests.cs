using System.Numerics;
using EidolonCore.Nexus.Sdf;
using EidolonCore.Nexus.Xpbd;

namespace Eidolon.Tests;

public class RigidBodyTests
{
    private const float Dt = 1f / 60f;

    private static float MinPairDistance(XpbdSolver solver, int[] corners) =>
        corners.SelectMany((a, i) => corners.Skip(i + 1), (a, b) => (a, b))
               .Min(pair => Vector3.Distance(solver.Particles[pair.a].Position, solver.Particles[pair.b].Position));

    private static SdfContactConstraint Floor(XpbdBodyGeometry body) =>
        new(new SdfPlane(Vector3.Zero, Vector3.UnitY), body.ContactRadius, body.ContactParticles);

    [Fact]
    public void BoxFallsAsARigidBody()
    {
        var solver = new XpbdSolver();
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 10f, 0f), new Vector3(0.5f));

        var restMin = MinPairDistance(solver, body.ContactParticles);

        for (var step = 0; step < 60; step++)
            solver.Step(Dt);

        // The whole body accelerates together, keeping its shape.
        Assert.InRange(body.Shape.Centroid.Y, 4.8f, 5.2f);
        Assert.Equal(restMin, MinPairDistance(solver, body.ContactParticles), 2);
        Assert.Equal(Quaternion.Identity, body.Shape.Rotation, new QuaternionComparer(0.01f));
    }

    [Fact]
    public void BoxRestsOnTheSurface()
    {
        var solver = new XpbdSolver { Iterations = 8 };
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 2f, 0f), new Vector3(0.5f));

        solver.AddConstraint(Floor(body));

        for (var step = 0; step < 240; step++)
            solver.Step(Dt);

        // Bottom face sits on the plane: centroid is half a box plus the contact radius.
        Assert.InRange(body.Shape.Centroid.Y, 0.45f, 0.62f);
        Assert.Equal(Quaternion.Identity, body.Shape.Rotation, new QuaternionComparer(0.05f));
    }

    [Fact]
    public void RotationIsRecoveredFromTheParticles()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 6f);

        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, new Vector3(1f, 2f, 3f), new Vector3(0.5f), rotation);

        solver.Step(Dt);

        VectorAssert.Near(new Vector3(1f, 2f, 3f), body.Shape.Centroid);
        Assert.Equal(rotation, body.Shape.Rotation, new QuaternionComparer(0.01f));
    }

    [Fact]
    public void TransformMapsLocalSpaceToWorld()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);

        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, new Vector3(-2f, 1f, 0.5f), new Vector3(0.25f, 0.5f, 1f), rotation);

        solver.Step(Dt);

        var localCorner = new Vector3(0.25f, 0.5f, 1f);
        var expected = body.Shape.Centroid + Vector3.Transform(localCorner, rotation);
        var actual = Vector3.Transform(localCorner, body.Shape.Transform);

        VectorAssert.Near(expected, actual, 0.01f);
    }


    [Fact]
    public void PureTranslationHasNoSpin()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, Vector3.Zero, new Vector3(0.5f));

        for (var index = 0; index < body.ContactParticles.Length; index++)
        {
            var particle = body.ContactParticles[index];
            solver.Particle(particle).Velocity = new Vector3(2f, 0f, 0f);
        }

        solver.Step(Dt);

        VectorAssert.Near(new Vector3(2f, 0f, 0f), body.Shape.LinearVelocity, 0.01f);
        Assert.True(body.Shape.AngularVelocity.Length() < 0.01f,
            $"expected no spin, got {body.Shape.AngularVelocity}");
    }

    [Fact]
    public void AngularVelocityIsRecoveredFromParticleVelocities()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, Vector3.Zero, new Vector3(0.5f));

        // Spin the points about Y: v = w x r.
        var spin = new Vector3(0f, 3f, 0f);

        foreach (var index in body.ContactParticles)
        {
            var r = solver.Particles[index].Position;
            solver.Particle(index).Velocity = Vector3.Cross(spin, r);
        }

        solver.Step(Dt);

        VectorAssert.Near(spin, body.Shape.AngularVelocity, 0.1f);
        Assert.True(body.Shape.LinearVelocity.Length() < 0.05f,
            $"expected no drift, got {body.Shape.LinearVelocity}");
    }

    [Fact]
    public void OffCentreContactStartsARotation()
    {
        var solver = new XpbdSolver { Iterations = 8 };

        // Tilted box dropped on a floor: it lands on one edge and should start rotating.
        var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.6f);
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f), tilt);

        solver.AddConstraint(Floor(body));

        for (var step = 0; step < 25; step++)
            solver.Step(Dt);

        Assert.True(body.Shape.AngularVelocity.Length() > 0.05f,
            $"expected the tilted body to rotate, angular velocity was {body.Shape.AngularVelocity.Length()}");
    }

    [Fact]
    public void SphereCollidesWithItsOwnRadius()
    {
        const float radius = 0.3f;

        var solver = new XpbdSolver { Iterations = 8 };
        var body = XpbdBodies.AddSphere(solver, new Vector3(0f, 2f, 0f), radius);

        Assert.Equal(radius, body.ContactRadius);
        Assert.Single(body.ContactParticles);

        solver.AddConstraint(Floor(body));

        for (var step = 0; step < 240; step++)
            solver.Step(Dt);

        // A sphere is one particle with the sphere's radius, so it rests exactly one radius up.
        Assert.InRange(body.Shape.Centroid.Y, radius - 0.01f, radius + 0.01f);
    }

    [Fact]
    public void SphereReportsAPositionEvenWithNoOrientationToRecover()
    {
        var solver = new XpbdSolver();
        var body = XpbdBodies.AddSphere(solver, new Vector3(1f, 5f, -2f), 0.25f);

        for (var step = 0; step < 30; step++)
            solver.Step(Dt);

        // The single particle *is* the sphere: its position is the body's position even though no
        // rotation can be fitted to one point.
        VectorAssert.Near(solver.Particles[body.ContactParticles[0]].Position, body.Shape.Centroid);
        Assert.True(body.Shape.Centroid.Y < 5f, "expected the sphere to have fallen");
        Assert.Equal(Quaternion.Identity, body.Shape.Rotation);
    }

    [Fact]
    public void CapsuleRestsOnItsSide()
    {
        const float radius = 0.25f;
        const float segment = 1f;

        var solver = new XpbdSolver { Iterations = 8 };

        // Laid on its side: the axis is horizontal, so both contact particles touch at once.
        var body = XpbdBodies.AddCapsule(solver, new Vector3(0f, 3f, 0f), radius, segment,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f));

        solver.AddConstraint(Floor(body));

        for (var step = 0; step < 240; step++)
            solver.Step(Dt);

        // Lying on the floor, the capsule's centre is exactly one radius up.
        Assert.InRange(body.Shape.Centroid.Y, radius - 0.04f, radius + 0.04f);
    }

    [Fact]
    public void CapsuleRotationIsRecoveredFromItsFrameParticles()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f);

        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddCapsule(solver, new Vector3(2f, 1f, 0f), 0.2f, 0.8f, rotation);

        solver.Step(Dt);

        // Two particles on the axis alone are collinear, so the off-axis particles are what make the
        // rotation recoverable.
        Assert.Equal(rotation, body.Shape.Rotation, new QuaternionComparer(0.01f));
    }

    [Fact]
    public void SqueezedBodySpringsBackIntoShape()
    {
        // Spawn a box half buried in the floor: every particle starts under the surface, so the
        // contacts push the whole cloud flat on one plane. That flattens the covariance enough to
        // leave it degenerate, and the body must keep its shape anyway.
        var rotation = Quaternion.Normalize(new Quaternion(0.41f, 0.48f, -0.58f, 0.51f));

        var solver = new XpbdSolver { Damping = 0.6f };
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, -0.48f, 0f), new Vector3(0.5f), rotation);
        solver.AddConstraint(Floor(body));

        for (var step = 0; step < 600; step++)
            solver.Step(Dt);

        var lowest = body.ContactParticles.Min(index => solver.Particles[index].Position.Y);
        var highest = body.ContactParticles.Max(index => solver.Particles[index].Position.Y);

        // The floor helper puts the plane at y = 0, so the lowest particle sits one contact radius up.
        Assert.InRange(lowest, body.ContactRadius - 0.02f, body.ContactRadius + 0.02f);
        Assert.True(highest - lowest > 0.9f, $"the box collapsed to {highest - lowest:F2} units tall");
    }

    private sealed class QuaternionComparer(float tolerance) : IEqualityComparer<Quaternion>
    {
        public bool Equals(Quaternion x, Quaternion y) =>
            MathF.Abs(Quaternion.Dot(Quaternion.Normalize(x), Quaternion.Normalize(y))) >= 1f - tolerance;

        public int GetHashCode(Quaternion obj) => obj.GetHashCode();
    }
}
