using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus.Sdf;
using EidolonCore.Nexus.Xpbd;
using EidolonCore.Simulation;

namespace EidolonCore.Nexus;

/// <summary>The analytic shape a body collides as.</summary>
public enum XpbdShape
{
    Box,
    Sphere,
    Capsule,
}

/// <summary>Marks an entity as a rigid body driven by XPBD.</summary>
public sealed class XpbdBody : IComponent
{
    /// <summary>
    /// Which shape the body collides as. The shape is the body's own geometry: it decides both the
    /// particles that ride the surface and the surface other bodies query.
    /// </summary>
    public XpbdShape Shape { get; set; } = XpbdShape.Box;

    /// <summary>Half extents of a box body. The transform's scale is not baked in, so this is world units.</summary>
    public Vector3 HalfExtents { get; set; } = new(0.5f);

    /// <summary>Radius of a sphere or capsule body.</summary>
    public float Radius { get; set; } = 0.5f;

    /// <summary>Length of a capsule's axis between its cap centres, caps excluded.</summary>
    public float SegmentLength { get; set; } = 1f;

    /// <summary>Slack a box's corner particles keep from a surface. Sphere and capsule bodies use their radius instead.</summary>
    public float ContactRadius { get; set; } = 0.02f;

    public float InverseMass { get; set; } = 1f;

    public float Stiffness { get; set; } = 1f;

    /// <summary>The body's collision shape in its own space, centred on the transform's origin.</summary>
    public ISdf CreateShape() => Shape switch
    {
        XpbdShape.Sphere => new SdfSphere(Vector3.Zero, Radius),

        XpbdShape.Capsule => new SdfCapsule(
            new Vector3(0f, -SegmentLength * 0.5f, 0f),
            new Vector3(0f, SegmentLength * 0.5f, 0f),
            Radius),

        _ => new SdfBox(Vector3.Zero, HalfExtents),
    };
}

/// <summary>
/// Runs XPBD for the entities carrying an <see cref="XpbdBody"/> and writes the solved transforms back
/// to the world.
/// </summary>
/// <remarks>
/// <para>
/// Every body is an SDF. Its shape answers "where is the surface" for any other body's particles, so a
/// box, a sphere and a capsule all collide through one code path: one contact constraint against the
/// floor plane, plus one per ordered pair of bodies.
/// </para>
/// <para>
/// A contact only moves the body that owns the particles, so a body is pushed out of another but does
/// not push back within the same projection. Without a contact manifold or impulse sharing that keeps
/// stacking stable enough to look at, at the cost of conserving momentum: this is a prototyped
/// behaviour, not a promise.
/// </para>
/// <para>
/// The simulation is rebuilt when the body set or any shape parameter changes, and whenever the world
/// instance differs from the one the solver was built against. Play mode clones the world and keeps
/// entity ids, so identity rather than contents decides whether a rebuild is needed.
/// </para>
/// </remarks>
public sealed class XpbdWorldSystem : ISystem
{
    private sealed class TrackedBody
    {
        public Entity Entity;
        public float InverseMass;
        public XpbdBodyGeometry Geometry = default;
        public SdfTransformed Field = null!;
    }

    private readonly List<TrackedBody> _bodies = [];

    private XpbdSolver? _solver;
    private World? _solverWorld;
    private int _signature;

    /// <summary>Height of the SDF floor plane. Defaults to the demo scene's ground.</summary>
    public float FloorHeight { get; set; } = -0.5f;

    public int BodyCount => _bodies.Count;

    public void Update(World world, in FrameTime time)
    {
        SyncBodies(world);

        if (_solver is null || _bodies.Count == 0)
            return;

        _solver.Step(time.Delta);

        foreach (var body in _bodies)
        {
            var shape = body.Geometry.Shape;

            // Publish the solved pose so next step's contacts query the body where it now is.
            body.Field.SetPose(shape.Centroid, shape.Rotation);

            if (!world.TryGetComponent<Transform>(body.Entity, out var transform))
                continue;

            transform.TrySetWorldPose(world, shape.Centroid, shape.Rotation);
            world.SetComponent(body.Entity, transform);
        }
    }

    private void SyncBodies(World world)
    {
        var current = new List<Entity>();
        var hash = new HashCode();

        foreach (var entity in world.GetEntitiesWith<Transform, XpbdBody>())
        {
            current.Add(entity);

            var body = world.GetComponent<XpbdBody>(entity);
            hash.Add(entity.Id);
            hash.Add((int)body.Shape);
            hash.Add(body.HalfExtents);
            hash.Add(body.Radius);
            hash.Add(body.SegmentLength);
            hash.Add(body.ContactRadius);
            hash.Add(body.InverseMass);
            hash.Add(body.Stiffness);
        }

        var signature = hash.ToHashCode();

        if (_solver is not null && ReferenceEquals(world, _solverWorld) && signature == _signature)
            return;

        Rebuild(world, current, signature);
    }

    private void Rebuild(World world, List<Entity> entities, int signature)
    {
        _solverWorld = world;
        _signature = signature;
        _bodies.Clear();

        // Damping substitutes for friction, which contacts do not model yet; without it a box pushed
        // along the floor would slide forever.
        _solver = new XpbdSolver
        {
            Gravity = new Vector3(0f, -9.81f, 0f),
            Iterations = 8,
            Damping = 0.6f,
        };

        var floor = new SdfPlane(new Vector3(0f, FloorHeight, 0f), Vector3.UnitY);

        foreach (var entity in entities)
        {
            var transform = world.GetComponent<Transform>(entity);
            var body = world.GetComponent<XpbdBody>(entity);

            var position = transform.GetWorldPosition(world);
            var rotation = transform.GetWorldRotation(world);

            var geometry = body.Shape switch
            {
                XpbdShape.Sphere => XpbdBodies.AddSphere(
                    _solver, position, body.Radius, body.InverseMass, body.Stiffness),

                XpbdShape.Capsule => XpbdBodies.AddCapsule(
                    _solver, position, body.Radius, body.SegmentLength, rotation, body.InverseMass, body.Stiffness),

                _ => XpbdBodies.AddBox(
                    _solver, position, body.HalfExtents, rotation, body.InverseMass, body.Stiffness,
                    body.ContactRadius),
            };

            // A static body (zero inverse mass) pins its particles, so no contact can move it.
            if (body.InverseMass > 0f)
            {
                _solver.AddConstraint(new SdfContactConstraint(
                    floor, geometry.ContactRadius, geometry.ContactParticles));
            }

            var field = new SdfTransformed(body.CreateShape());
            field.SetPose(position, rotation);

            _bodies.Add(new TrackedBody
            {
                Entity = entity,
                InverseMass = body.InverseMass,
                Geometry = geometry,
                Field = field,
            });
        }

        // Every dynamic body is kept out of every other body's shape, static or not.
        foreach (var body in _bodies)
        {
            if (body.InverseMass <= 0f)
                continue;

            foreach (var other in _bodies)
            {
                if (ReferenceEquals(body, other))
                    continue;

                _solver.AddConstraint(new SdfContactConstraint(
                    other.Field, body.Geometry.ContactRadius, body.Geometry.ContactParticles));
            }
        }
    }
}
