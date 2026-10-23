using System.Runtime.InteropServices;

using System.Numerics;

namespace EidolonCore.Nexus.Xpbd;

/// <summary>
/// The state XPBD solves for: a point mass with a cached previous position, so velocity can be
/// recovered from the position change after constraints have moved it.
/// </summary>
/// <remarks>
/// Rigid bodies are built on top of this layer rather than beside it: a body owns particles with
/// shapes attached (mass, inertia, orientation) and adds constraints that couple them, so the
/// solver only ever needs positions and inverse masses.
/// </remarks>
public struct XpbdParticle
{
    public Vector3 Position;

    public Vector3 Velocity;

    /// <summary>Zero pins the particle: it never moves and makes a good anchor for joints.</summary>
    public float InverseMass;

    /// <summary>Position at the start of the step, used for the velocity update.</summary>
    public Vector3 PreviousPosition;

    public XpbdParticle(Vector3 position, float inverseMass = 1f)
    {
        Position = position;
        PreviousPosition = position;
        InverseMass = inverseMass;
        Velocity = Vector3.Zero;
    }

    public readonly bool IsPinned => InverseMass <= 0f;
}

/// <summary>A constraint the solver projects positions against.</summary>
public interface IXpbdConstraint
{
    /// <summary>Clears the accumulator at the start of a step.</summary>
    void BeginStep();

    /// <summary>Projects the particle positions once. Called once per solver iteration.</summary>
    void Solve(Span<XpbdParticle> particles, float deltaTime);
}

/// <summary>
/// Small position-based solver: integrate, project constraints, recover velocity.
/// </summary>
/// <remarks>
/// This is the centre of Nexus physics, deliberately kept free of SDF and rendering types. Contacts
/// against an SDF will be expressed as another <see cref="IXpbdConstraint"/>; rigid bodies as
/// particles plus orientation constraints.
/// </remarks>
public sealed class XpbdSolver
{
    private readonly List<XpbdParticle> _particles = [];
    private readonly List<IXpbdConstraint> _constraints = [];

    public Vector3 Gravity { get; set; } = new(0f, -9.81f, 0f);

    /// <summary>Solver iterations per step. More iterations means stiffer constraints.</summary>
    public int Iterations { get; set; } = 8;

    public IReadOnlyList<XpbdParticle> Particles => _particles;

    public IReadOnlyList<IXpbdConstraint> Constraints => _constraints;

    public int AddParticle(XpbdParticle particle)
    {
        _particles.Add(particle);
        return _particles.Count - 1;
    }

    public int AddParticle(Vector3 position, float inverseMass = 1f) =>
        AddParticle(new XpbdParticle(position, inverseMass));

    public void AddConstraint(IXpbdConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        _constraints.Add(constraint);
    }

    public ref XpbdParticle Particle(int index) => ref CollectionsMarshal.AsSpan(_particles)[index];

    /// <summary>Advances the simulation by one fixed step.</summary>
    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        var particles = CollectionsMarshal.AsSpan(_particles);

        foreach (ref var particle in particles)
        {
            particle.PreviousPosition = particle.Position;

            if (particle.IsPinned)
            {
                particle.Velocity = Vector3.Zero;
                continue;
            }

            particle.Velocity += Gravity * deltaTime;
            particle.Position += particle.Velocity * deltaTime;
        }

        foreach (var constraint in _constraints)
            constraint.BeginStep();

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            foreach (var constraint in _constraints)
                constraint.Solve(particles, deltaTime);
        }

        foreach (ref var particle in particles)
        {
            if (particle.IsPinned)
                continue;

            particle.Velocity = (particle.Position - particle.PreviousPosition) / deltaTime;
        }
    }
}

/// <summary>
/// Keeps two particles at a rest distance, with optional compliance (softness). The XPBD
/// accumulator makes the stiffness independent of iteration count.
/// </summary>
public sealed class DistanceConstraint(int a, int b, float restLength, float compliance = 0f) : IXpbdConstraint
{
    private float _lambda;

    public int A { get; } = a;

    public int B { get; } = b;

    public float RestLength { get; } = restLength;

    public float Compliance { get; } = compliance;

    public void BeginStep() => _lambda = 0f;

    public void Solve(Span<XpbdParticle> particles, float deltaTime)
    {
        ref var first = ref particles[A];
        ref var second = ref particles[B];

        var delta = second.Position - first.Position;
        var length = delta.Length();

        if (length < 1e-8f)
            return;

        var direction = delta / length;
        var constraint = length - RestLength;

        var alpha = Compliance / (deltaTime * deltaTime);
        var weight = first.InverseMass + second.InverseMass;

        if (weight <= 0f)
            return;

        var deltaLambda = (-constraint - alpha * _lambda) / (weight + alpha);
        _lambda += deltaLambda;

        first.Position -= direction * (first.InverseMass * deltaLambda);
        second.Position += direction * (second.InverseMass * deltaLambda);
    }
}
