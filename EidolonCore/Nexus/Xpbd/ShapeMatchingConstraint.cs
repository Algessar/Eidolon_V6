using System.Numerics;

namespace EidolonCore.Nexus.Xpbd;

/// <summary>
/// Holds a set of particles in a rigid (or semi-rigid) shape by shape matching: every iteration the
/// particles are rotated onto the best-fit transform of their rest shape and pulled towards it.
/// </summary>
/// <remarks>
/// Shape matching scales with particle count without adding pairwise constraints, which is why a
/// rigid body is modelled as a small cloud of particles here rather than as a separate solver path.
/// The recovered rotation is exposed so a renderer or compound-shape code can place the body.
/// </remarks>
public sealed class ShapeMatchingConstraint : IXpbdConstraint, IXpbdPostStep
{
    private const int PolarIterations = 8;
    private const float PolarTolerance = 1e-6f;

    private readonly int[] _indices;
    private readonly Vector3[] _rest;

    /// <summary>Rotation in the maths (column-vector) convention, i.e. world = R * rest.</summary>
    private Matrix4x4 _rotation = Matrix4x4.Identity;

    public ShapeMatchingConstraint(int[] indices, Vector3[] restOffsets, float stiffness = 1f)
    {
        if (indices.Length == 0)
            throw new ArgumentException("A body needs at least one particle.", nameof(indices));

        if (indices.Length != restOffsets.Length)
            throw new ArgumentException("Rest offsets must match the particle count.", nameof(restOffsets));

        if (stiffness <= 0f || stiffness > 1f)
            throw new ArgumentOutOfRangeException(nameof(stiffness), "Stiffness is a 0..1 blend per iteration.");

        _indices = indices;
        _rest = restOffsets;
        Stiffness = stiffness;
    }

    /// <summary>How far each iteration pulls particles towards the goal shape (1 = rigid).</summary>
    public float Stiffness { get; }

    /// <summary>Particle indices making up this body.</summary>
    public IReadOnlyList<int> ParticleIndices => _indices;

    /// <summary>World-space centroid of the body after the last solve.</summary>
    public Vector3 Centroid { get; private set; }

    /// <summary>Body rotation after the last solve.</summary>
    public Quaternion Rotation { get; private set; } = Quaternion.Identity;

    /// <summary>World transform mapping body-local space to world space.</summary>
    public Matrix4x4 Transform => Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Centroid);

    /// <summary>Linear velocity of the body centre, recovered after each step.</summary>
    public Vector3 LinearVelocity { get; private set; }

    /// <summary>Angular velocity about the body centre, recovered from particle momentum.</summary>
    public Vector3 AngularVelocity { get; private set; }

    /// <summary>
    /// Redistributes the recovered particle velocities onto a rigid motion: one linear velocity plus
    /// one angular velocity about the centroid. That is what gives a body inertia, so an off-centre
    /// contact spins it instead of just shifting every corner equally.
    /// </summary>
    public void AfterVelocityUpdate(Span<XpbdParticle> particles, float deltaTime)
    {
        var centroid = Vector3.Zero;
        var count = 0;
        var linear = Vector3.Zero;

        foreach (var index in _indices)
        {
            centroid += particles[index].Position;
            linear += particles[index].Velocity;
            count++;
        }

        if (count == 0)
            return;

        centroid /= count;
        linear /= count;

        // Angular momentum L = sum r x v and the point-set inertia tensor, both about the centroid.
        var momentum = Vector3.Zero;
        var inertia = default(Matrix4x4);

        foreach (var index in _indices)
        {
            var r = particles[index].Position - centroid;
            var v = particles[index].Velocity;

            momentum += Vector3.Cross(r, v);

            inertia.M11 += r.Y * r.Y + r.Z * r.Z;
            inertia.M22 += r.X * r.X + r.Z * r.Z;
            inertia.M33 += r.X * r.X + r.Y * r.Y;
            inertia.M12 -= r.X * r.Y;
            inertia.M21 -= r.X * r.Y;
            inertia.M13 -= r.X * r.Z;
            inertia.M31 -= r.X * r.Z;
            inertia.M23 -= r.Y * r.Z;
            inertia.M32 -= r.Y * r.Z;
        }

        inertia.M44 = 1f;

        var angular = Matrix4x4.Invert(inertia, out var inverseInertia)
            ? Vector3.Transform(momentum, inverseInertia)
            : Vector3.Zero;

        LinearVelocity = linear;
        AngularVelocity = angular;

        // Re-project the particle velocities so the next integration is consistent with the body.
        foreach (var index in _indices)
        {
            ref var particle = ref particles[index];

            if (particle.IsPinned)
                continue;

            particle.Velocity = linear + Vector3.Cross(angular, particle.Position - centroid);
        }
    }

    public void BeginStep(Span<XpbdParticle> particles, float deltaTime)
    {
    }

    public void Solve(Span<XpbdParticle> particles, float deltaTime)
    {
        // Current centroid of the body's particles.
        var centroid = Vector3.Zero;
        var count = 0;

        foreach (var index in _indices)
        {
            centroid += particles[index].Position;
            count++;
        }

        if (count == 0)
            return;

        centroid /= count;

        // Covariance between current offsets and rest offsets: A = sum (x - c) * q^T.
        var covariance = default(Matrix4x4);

        for (var slot = 0; slot < _indices.Length; slot++)
        {
            var current = particles[_indices[slot]].Position - centroid;
            var rest = _rest[slot];

            covariance.M11 += current.X * rest.X;
            covariance.M12 += current.X * rest.Y;
            covariance.M13 += current.X * rest.Z;
            covariance.M21 += current.Y * rest.X;
            covariance.M22 += current.Y * rest.Y;
            covariance.M23 += current.Y * rest.Z;
            covariance.M31 += current.Z * rest.X;
            covariance.M32 += current.Z * rest.Y;
            covariance.M33 += current.Z * rest.Z;
        }

        // The 4x4 identity corner has to be valid or Matrix4x4.Invert treats it as singular.
        covariance.M44 = 1f;

        if (!TryExtractRotation(covariance, out var rotation))
            return;

        _rotation = rotation;

        // System.Numerics transforms row vectors, so the maths-convention rotation is transposed.
        var rowRotation = Matrix4x4.Transpose(rotation);

        for (var slot = 0; slot < _indices.Length; slot++)
        {
            ref var particle = ref particles[_indices[slot]];

            if (particle.IsPinned)
                continue;

            var goal = centroid + Vector3.Transform(_rest[slot], rowRotation);
            particle.Position = Vector3.Lerp(particle.Position, goal, Stiffness);
        }

        Centroid = centroid;
        Rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rowRotation));
    }

    /// <summary>
    /// Polar decomposition by the Higham iteration: A = R S with R orthonormal. Converges quickly
    /// for the near-rigid covariances a body produces.
    /// </summary>
    private static bool TryExtractRotation(Matrix4x4 covariance, out Matrix4x4 rotation)
    {
        rotation = covariance;

        for (var iteration = 0; iteration < PolarIterations; iteration++)
        {
            if (!Matrix4x4.Invert(rotation, out var inverse))
                return false;

            var next = (rotation + Matrix4x4.Transpose(inverse)) * 0.5f;

            var delta = MathF.Abs(next.M11 - rotation.M11) + MathF.Abs(next.M22 - rotation.M22) +
                        MathF.Abs(next.M33 - rotation.M33) + MathF.Abs(next.M12 - rotation.M12) +
                        MathF.Abs(next.M13 - rotation.M13) + MathF.Abs(next.M21 - rotation.M21) +
                        MathF.Abs(next.M23 - rotation.M23) + MathF.Abs(next.M31 - rotation.M31) +
                        MathF.Abs(next.M32 - rotation.M32);

            rotation = next;

            if (delta < PolarTolerance)
                break;
        }

        return true;
    }
}

/// <summary>Creates the common body shapes as particle clouds plus their shape-matching constraint.</summary>
public static class XpbdBodies
{
    /// <summary>
    /// Adds the eight corners of a box to the solver and keeps them rigid. Rotation is recovered
    /// from the corners, so the body can tumble and be read back as a transform.
    /// </summary>
    public static ShapeMatchingConstraint AddBox(XpbdSolver solver, Vector3 center, Vector3 halfExtents,
        Quaternion? rotation = null, float particleInverseMass = 1f, float stiffness = 1f)
    {
        var bodyRotation = rotation ?? Quaternion.Identity;
        var indices = new int[8];
        var rest = new Vector3[8];

        for (var corner = 0; corner < 8; corner++)
        {
            var local = new Vector3(
                (corner & 1) == 0 ? -halfExtents.X : halfExtents.X,
                (corner & 2) == 0 ? -halfExtents.Y : halfExtents.Y,
                (corner & 4) == 0 ? -halfExtents.Z : halfExtents.Z);

            rest[corner] = local;
            indices[corner] = solver.AddParticle(center + Vector3.Transform(local, bodyRotation), particleInverseMass);
        }

        var constraint = new ShapeMatchingConstraint(indices, rest, stiffness);
        solver.AddConstraint(constraint);
        return constraint;
    }
}
