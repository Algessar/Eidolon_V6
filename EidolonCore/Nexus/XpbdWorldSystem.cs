using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus.Sdf;
using EidolonCore.Nexus.Xpbd;
using EidolonCore.Simulation;

namespace EidolonCore.Nexus;

/// <summary>Marks an entity as a rigid box driven by XPBD.</summary>
public sealed class XpbdBody : IComponent
{
    public Vector3 HalfExtents = new(0.5f);

    /// <summary>Radius the corners use for SDF contacts.</summary>
    public float ContactRadius = 0.02f;

    public float InverseMass = 1f;

    public float Stiffness = 1f;
}

/// <summary>
/// Runs XPBD for the entities carrying an <see cref="XpbdBody"/>, with an SDF floor, and writes the
/// solved transforms back to the world.
/// </summary>
/// <remarks>
/// Bodies are rebuilt whenever the set of body entities changes, which keeps play mode simple: one
/// solver per playing session, no incremental bookkeeping. The SDF floor lives here for now; a
/// scene-provided collision field replaces it once the editor can author one.
/// </remarks>
public sealed class XpbdWorldSystem : ISystem
{
    private readonly List<(Entity Entity, ShapeMatchingConstraint Body)> _bodies = [];
    private readonly List<Entity> _signature = [];

    private XpbdSolver? _solver;

    /// <summary>The world the current solver was built from; a different instance means a new session.</summary>
    private World? _solverWorld;

    /// <summary>Height of the SDF floor plane. Defaults to the demo scene's ground.</summary>
    public float FloorHeight { get; set; } = -0.5f;

    public int BodyCount => _bodies.Count;

    public void Update(World world, in FrameTime time)
    {
        SyncBodies(world);

        if (_solver is null || _bodies.Count == 0)
            return;

        _solver.Step(time.Delta);

        foreach (var (entity, body) in _bodies)
        {
            if (!world.TryGetComponent<Transform>(entity, out var transform))
                continue;

            transform.Position = body.Centroid;
            transform.Rotation = body.Rotation;
            world.SetComponent(entity, transform);
        }
    }

    private void SyncBodies(World world)
    {
        var current = new List<Entity>();

        foreach (var entity in world.GetEntitiesWith<Transform, XpbdBody>())
            current.Add(entity);

        // Stop discards the runtime world, so a new play session hands us a fresh instance. Entity ids
        // are preserved by the clone, which means the entity set can look unchanged while every
        // particle index now points into the old, discarded simulation.
        if (_solver is not null && ReferenceEquals(world, _solverWorld) && SameSet(current, _signature))
            return;

        _solverWorld = world;

        _signature.Clear();
        _signature.AddRange(current);
        _bodies.Clear();

        _solver = new XpbdSolver { Gravity = new Vector3(0f, -9.81f, 0f), Iterations = 8 };
        var floor = new SdfPlane(new Vector3(0f, FloorHeight, 0f), Vector3.UnitY);

        foreach (var entity in current)
        {
            var transform = world.GetComponent<Transform>(entity);
            var body = world.GetComponent<XpbdBody>(entity);

            var matrix = transform.GetWorldMatrix(world);
            var position = new Vector3(matrix.M41, matrix.M42, matrix.M43);
            var rotation = Matrix4x4.Decompose(matrix, out _, out var decomposed, out _)
                ? decomposed
                : Quaternion.Identity;

            var constraint = XpbdBodies.AddBox(_solver, position, body.HalfExtents, rotation,
                body.InverseMass, body.Stiffness);

            _solver.AddConstraint(new SdfContactConstraint(floor, body.ContactRadius,
                constraint.ParticleIndices.ToArray()));

            _bodies.Add((entity, constraint));
        }
    }

    private static bool SameSet(List<Entity> a, List<Entity> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
