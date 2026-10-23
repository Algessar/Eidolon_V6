using System.Numerics;
using EidolonCore.ECS;

namespace EidolonCore.Simulation;

/// <summary>
/// Spins the entity around an axis, as a minimal example of gameplay behaviour driven by
/// <see cref="ISystem"/> and <see cref="FrameTime"/>.
/// </summary>
public sealed class Spin : IComponent
{
    public Vector3 Axis = Vector3.UnitY;

    public float RadiansPerSecond = 1f;
}

/// <summary>
/// Applies <see cref="Spin"/> to every entity that has one. Only runs while play mode is ticking,
/// so the authored rotation is untouched when the session stops.
/// </summary>
public sealed class SpinSystem : ISystem
{
    public void Update(World world, in FrameTime time)
    {
        foreach (var entity in world.GetEntitiesWith<Transform, Spin>())
        {
            var spin = world.GetComponent<Spin>(entity);

            if (spin.Axis.LengthSquared() < 1e-8f || spin.RadiansPerSecond == 0f)
                continue;

            var axis = Vector3.Normalize(spin.Axis);
            var step = Quaternion.CreateFromAxisAngle(axis, spin.RadiansPerSecond * time.Delta);

            ref var transform = ref world.GetComponent<Transform>(entity);
            transform.Rotation = Quaternion.Normalize(step * transform.Rotation);
        }
    }
}
