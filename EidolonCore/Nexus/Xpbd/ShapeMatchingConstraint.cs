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

        // The centroid stands even when the rotation cannot be recovered: a body of particles that
        // cannot span a volume (a single-particle sphere) has no orientation, but it still has to
        // report where it is.
        Centroid = centroid;

        // Particles on one line or in one plane have no unique orientation, and the polar iteration
        // would turn rounding noise into a plausible-looking but wrong rotation. Squeezed clouds do
        // happen - contacts against a flat surface push particles into a plane - so the rotation is
        // simply left as it was. The projection below still runs, which is what lets the body spring
        // back into shape instead of collapsing for good.
        var trace = covariance.M11 + covariance.M22 + covariance.M33;
        var spansVolume = trace > 1e-12f &&
                          MathF.Abs(covariance.GetDeterminant()) > 1e-8f * trace * trace * trace;

        if (spansVolume && TryExtractRotation(covariance, out var recovered))
            _rotation = recovered;

        // System.Numerics transforms row vectors, so the maths-convention rotation is transposed.
        var rowRotation = Matrix4x4.Transpose(_rotation);

        for (var slot = 0; slot < _indices.Length; slot++)
        {
            ref var particle = ref particles[_indices[slot]];

            if (particle.IsPinned)
                continue;

            var goal = centroid + Vector3.Transform(_rest[slot], rowRotation);
            particle.Position = Vector3.Lerp(particle.Position, goal, Stiffness);
        }

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

/// <summary>
/// What the solver needs to treat a body as a collision shape: the rigid constraint holding its
/// particles, the particles that ride on its surface, and the radius those particles collide with.
/// </summary>
/// <remarks>
/// The contact radius differs by shape because the particle layout does. A box is approximated by a
/// lattice of points, so the radius is a small slack that keeps them just off a surface; a sphere and a
/// capsule are swept spheres, so their radius is the shape's own radius and the contact is exact.
/// </remarks>
public readonly record struct XpbdBodyGeometry(ShapeMatchingConstraint Shape, int[] ContactParticles,
    float ContactRadius);

/// <summary>Creates the common body shapes as particle clouds plus their shape-matching constraint.</summary>
public static class XpbdBodies
{
    /// <summary>Particles per axis in a box body, i.e. corners, edges, faces and centre are all sampled.</summary>
    private const int BoxLattice = 3;
    /// <summary>
    /// Adds a box to the solver as a three-by-three-by-three lattice of particles and keeps them rigid.
    /// Rotation is recovered from the lattice, so the body can tumble and be read back as a transform.
    /// </summary>
    /// <remarks>
    /// The lattice matters for collision, not just for mass: corners alone leave the middle of a face
    /// unsampled, so a box would sink through anything smaller than itself that sits under its centre.
    /// Face and edge points are what let it rest on a platform it overhangs.
    /// </remarks>
    public static XpbdBodyGeometry AddBox(XpbdSolver solver, Vector3 center, Vector3 halfExtents,
        Quaternion? rotation = null, float particleInverseMass = 1f, float stiffness = 1f,
        float contactRadius = 0.02f)
    {
        var bodyRotation = rotation ?? Quaternion.Identity;
        var indices = new int[BoxLattice * BoxLattice * BoxLattice];
        var rest = new Vector3[indices.Length];
        var slot = 0;

        for (var x = 0; x < BoxLattice; x++)
        {
            for (var y = 0; y < BoxLattice; y++)
            {
                for (var z = 0; z < BoxLattice; z++)
                {
                    var local = new Vector3(
                        AxisOffset(x) * halfExtents.X,
                        AxisOffset(y) * halfExtents.Y,
                        AxisOffset(z) * halfExtents.Z);

                    rest[slot] = local;
                    indices[slot] = solver.AddParticle(
                        center + Vector3.Transform(local, bodyRotation), particleInverseMass);

                    slot++;
                }
            }
        }

        var constraint = new ShapeMatchingConstraint(indices, rest, stiffness);
        solver.AddConstraint(constraint);

        return new XpbdBodyGeometry(constraint, indices, contactRadius);
    }

    /// <summary>-1, 0 or +1 across the lattice axis.</summary>
    private static float AxisOffset(int step) => step - 1;

    /// <summary>
    /// Adds a sphere as a single particle at its centre. The particle's contact radius is the sphere's
    /// radius, so a sphere collides exactly rather than approximately, and it has no orientation to
    /// track because a sphere rotating is indistinguishable from one that is not.
    /// </summary>
    public static XpbdBodyGeometry AddSphere(XpbdSolver solver, Vector3 center, float radius,
        float particleInverseMass = 1f, float stiffness = 1f)
    {
        if (radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        var indices = new[] { solver.AddParticle(center, particleInverseMass) };
        var constraint = new ShapeMatchingConstraint(indices, [Vector3.Zero], stiffness);
        solver.AddConstraint(constraint);

        return new XpbdBodyGeometry(constraint, indices, radius);
    }

    /// <summary>
    /// Adds a capsule as two end-of-segment particles, each colliding with the capsule's radius: the
    /// swept spheres along that segment are exactly the capsule, so contacts are exact and the body
    /// may tip and roll freely.
    /// </summary>
    /// <param name="segmentLength">Length of the axis between the two cap centres, caps excluded.</param>
    public static XpbdBodyGeometry AddCapsule(XpbdSolver solver, Vector3 center, float radius,
        float segmentLength, Quaternion? rotation = null, float particleInverseMass = 1f, float stiffness = 1f)
    {
        if (radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        if (segmentLength < 0f)
            throw new ArgumentOutOfRangeException(nameof(segmentLength));

        var bodyRotation = rotation ?? Quaternion.Identity;
        var half = segmentLength * 0.5f;

        // Two particles on the surface carry the contacts. The other four sit on the caps' equators and
        // carry no contacts at all: they are there so the point set spans a volume, which is what lets
        // the shape match recover a rotation for a capsule that tips or rolls. They are arranged
        // symmetrically so the body's centroid stays the capsule's own centre.
        Vector3[] rest =
        [
            new(0f, -half, 0f),
            new(0f, half, 0f),
            new(radius, half, 0f),
            new(-radius, half, 0f),
            new(0f, -half, radius),
            new(0f, -half, -radius),
        ];

        var indices = new int[rest.Length];

        for (var slot = 0; slot < rest.Length; slot++)
            indices[slot] = solver.AddParticle(center + Vector3.Transform(rest[slot], bodyRotation), particleInverseMass);

        var constraint = new ShapeMatchingConstraint(indices, rest, stiffness);
        solver.AddConstraint(constraint);

        return new XpbdBodyGeometry(constraint, [indices[0], indices[1]], radius);
    }
}
