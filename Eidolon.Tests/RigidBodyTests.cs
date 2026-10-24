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

    [Fact]
    public void BoxFallsAsARigidBody()
    {
        var solver = new XpbdSolver();
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 10f, 0f), new Vector3(0.5f));

        var corners = Enumerable.Range(0, 8).ToArray();
        var restMin = MinPairDistance(solver, corners);

        for (var step = 0; step < 60; step++)
            solver.Step(Dt);

        // The whole body accelerates together, keeping its shape.
        Assert.InRange(body.Centroid.Y, 4.8f, 5.2f);
        Assert.Equal(restMin, MinPairDistance(solver, corners), 2);
        Assert.Equal(Quaternion.Identity, body.Rotation, new QuaternionComparer(0.01f));
    }

    [Fact]
    public void BoxRestsOnTheSurface()
    {
        var solver = new XpbdSolver { Iterations = 8 };
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 2f, 0f), new Vector3(0.5f));

        solver.AddConstraint(new SdfContactConstraint(
            new SdfPlane(Vector3.Zero, Vector3.UnitY), 0.02f, Enumerable.Range(0, 8).ToArray()));

        for (var step = 0; step < 240; step++)
            solver.Step(Dt);

        // Bottom face sits on the plane: centroid is half a box plus the contact radius.
        Assert.InRange(body.Centroid.Y, 0.45f, 0.62f);
        Assert.Equal(Quaternion.Identity, body.Rotation, new QuaternionComparer(0.05f));
    }

    [Fact]
    public void RotationIsRecoveredFromTheParticles()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 6f);

        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, new Vector3(1f, 2f, 3f), new Vector3(0.5f), rotation);

        solver.Step(Dt);

        VectorAssert.Near(new Vector3(1f, 2f, 3f), body.Centroid);
        Assert.Equal(rotation, body.Rotation, new QuaternionComparer(0.01f));
    }

    [Fact]
    public void TransformMapsLocalSpaceToWorld()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);

        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, new Vector3(-2f, 1f, 0.5f), new Vector3(0.25f, 0.5f, 1f), rotation);

        solver.Step(Dt);

        var localCorner = new Vector3(0.25f, 0.5f, 1f);
        var expected = body.Centroid + Vector3.Transform(localCorner, rotation);
        var actual = Vector3.Transform(localCorner, body.Transform);

        VectorAssert.Near(expected, actual, 0.01f);
    }


    [Fact]
    public void PureTranslationHasNoSpin()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, Vector3.Zero, new Vector3(0.5f));

        for (var corner = 0; corner < 8; corner++)
            solver.Particle(corner).Velocity = new Vector3(2f, 0f, 0f);

        solver.Step(Dt);

        VectorAssert.Near(new Vector3(2f, 0f, 0f), body.LinearVelocity, 0.01f);
        Assert.True(body.AngularVelocity.Length() < 0.01f, $"expected no spin, got {body.AngularVelocity}");
    }

    [Fact]
    public void AngularVelocityIsRecoveredFromParticleVelocities()
    {
        var solver = new XpbdSolver { Gravity = Vector3.Zero };
        var body = XpbdBodies.AddBox(solver, Vector3.Zero, new Vector3(0.5f));

        // Spin the corners about Y: v = w x r.
        var spin = new Vector3(0f, 3f, 0f);

        for (var corner = 0; corner < 8; corner++)
        {
            var r = solver.Particles[corner].Position;
            solver.Particle(corner).Velocity = Vector3.Cross(spin, r);
        }

        solver.Step(Dt);

        VectorAssert.Near(spin, body.AngularVelocity, 0.1f);
        Assert.True(body.LinearVelocity.Length() < 0.05f, $"expected no drift, got {body.LinearVelocity}");
    }

    [Fact]
    public void OffCentreContactStartsARotation()
    {
        var solver = new XpbdSolver { Iterations = 8 };

        // Tilted box dropped on a floor: it lands on one edge and should start rotating.
        var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.6f);
        var body = XpbdBodies.AddBox(solver, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f), tilt);

        solver.AddConstraint(new SdfContactConstraint(
            new SdfPlane(Vector3.Zero, Vector3.UnitY), 0.02f, Enumerable.Range(0, 8).ToArray()));

        for (var step = 0; step < 25; step++)
            solver.Step(Dt);

        Assert.True(body.AngularVelocity.Length() > 0.05f,
            $"expected the tilted body to rotate, angular velocity was {body.AngularVelocity.Length()}");
    }

    private sealed class QuaternionComparer(float tolerance) : IEqualityComparer<Quaternion>
    {
        public bool Equals(Quaternion x, Quaternion y) =>
            MathF.Abs(Quaternion.Dot(Quaternion.Normalize(x), Quaternion.Normalize(y))) >= 1f - tolerance;

        public int GetHashCode(Quaternion obj) => obj.GetHashCode();
    }
}
