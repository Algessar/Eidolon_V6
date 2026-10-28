using System.Numerics;
using EidolonCore.Nexus.Sdf;

namespace EidolonCore.Nexus.Xpbd;

/// <summary>
/// Keeps particles of <see cref="Radius"/> outside an SDF surface. This is the join between the two
/// subsystems: SDF answers "where is the surface", XPBD decides how to satisfy it.
/// </summary>
/// <remarks>
/// Which particles are in contact is decided once per step (after integration); the projection then
/// re-samples the field each iteration so multiple solver iterations converge onto the surface
/// instead of fighting a stale normal. Compliance is per contact, so soft contacts are a tunable.
/// </remarks>
public sealed class SdfContactConstraint : IXpbdConstraint
{
    private readonly int[] _particles;
    private readonly List<int> _active = [];
    private float[] _lambda;

    public SdfContactConstraint(ISdf field, float radius, params int[] particles)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        _particles = particles ?? [];
        _lambda = new float[_particles.Length];

        if (radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));

        Radius = radius;
    }

    public ISdf Field { get; }

    /// <summary>Radius of the spheres the particles are treated as.</summary>
    public float Radius { get; }

    /// <summary>Softness of the contact. Zero is a rigid surface.</summary>
    public float Compliance { get; set; }

    /// <summary>Indexes of the particles that were in contact at the start of the step.</summary>
    public IReadOnlyList<int> ActiveContacts => _active;

    public void BeginStep(Span<XpbdParticle> particles, float deltaTime)
    {
        _active.Clear();

        for (var slot = 0; slot < _particles.Length; slot++)
        {
            _lambda[slot] = 0f;

            var index = _particles[slot];

            if (index < 0 || index >= particles.Length || particles[index].IsPinned)
                continue;

            if (Field.Distance(particles[index].Position) < Radius)
                _active.Add(slot);
        }
    }

    public void Solve(Span<XpbdParticle> particles, float deltaTime)
    {
        var alpha = Compliance / (deltaTime * deltaTime);

        foreach (var slot in _active)
        {
            var index = _particles[slot];

            if (index < 0 || index >= particles.Length)
                continue;

            ref var particle = ref particles[index];

            if (particle.IsPinned)
                continue;

            var sample = Field.Sample(particle.Position);
            var separation = sample.Distance - Radius;

            if (separation >= 0f)
                continue;

            var weight = particle.InverseMass;

            if (weight <= 0f)
                continue;

            var deltaLambda = (-separation - alpha * _lambda[slot]) / (weight + alpha);
            _lambda[slot] += deltaLambda;

            particle.Position += sample.Normal * (weight * deltaLambda);
        }
    }
}
