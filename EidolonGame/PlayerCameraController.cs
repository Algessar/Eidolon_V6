using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace EidolonGame;

/// <summary>
/// Flies the camera entity: WASD moves along the view, Q/E drop and rise, right-drag looks around,
/// and shift sprints.
/// </summary>
/// <remarks>
/// Movement is written to the entity's transform, not to the camera component. The camera then
/// follows through the usual transform sync, so the gizmo, the serializer and this script all agree
/// that the transform is the camera's state. World-space accessors are used so a parented camera
/// behaves the same as a root one.
/// </remarks>
public sealed class PlayerCameraController : IScript
{
    /// <summary>Metres per second at a walk.</summary>
    public float Speed { get; set; } = 4f;

    /// <summary>Multiplier applied while the sprint key is held.</summary>
    public float SprintMultiplier { get; set; } = 3f;

    /// <summary>Radians of look per pixel of mouse movement.</summary>
    public float LookSpeed { get; set; } = 0.0045f;

    /// <summary>How far the view may tilt before it would flip over the pole.</summary>
    public const float MaxPitch = 1.52f;

    private float _yaw;
    private float _pitch;

    public void Start(Entity self, World world)
    {
        // Pick up control looking where the camera was authored to look, rather than snapping to -Z.
        _yaw = 0f;
        _pitch = 0f;

        if (!world.TryGetComponent<Camera>(self, out var camera))
            return;

        var view = camera.Target - camera.Position;

        if (view.LengthSquared() < 1e-6f)
            return;

        var direction = Vector3.Normalize(view);
        _yaw = MathF.Atan2(-direction.X, -direction.Z);
        _pitch = MathF.Asin(Math.Clamp(-direction.Y, -1f, 1f));
    }

    public void Update(Entity self, World world, in FrameTime time, in GameInput input)
    {
        if (!world.TryGetComponent<Transform>(self, out var transform))
            return;

        var rotation = transform.GetWorldRotation(world);
        var position = transform.GetWorldPosition(world);
        var changed = false;

        if (input.RightDown && input.MouseDelta.LengthSquared() > 0f)
        {
            _yaw -= input.MouseDelta.X * LookSpeed;
            _pitch = Math.Clamp(_pitch + input.MouseDelta.Y * LookSpeed, -MaxPitch, MaxPitch);
            rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
            changed = true;
        }

        var forward = CameraOrientation.ForwardFrom(rotation);

        // Movement is horizontal: looking at the floor should not make W dive into it.
        var flatForward = new Vector3(forward.X, 0f, forward.Z);
        flatForward = flatForward.LengthSquared() > 1e-6f ? Vector3.Normalize(flatForward) : Vector3.UnitZ;
        var right = Vector3.Normalize(Vector3.Cross(flatForward, Vector3.UnitY));

        var move = Vector3.Zero;

        if (input.IsDown(GameKey.W))
            move += flatForward;

        if (input.IsDown(GameKey.S))
            move -= flatForward;

        if (input.IsDown(GameKey.D))
            move += right;

        if (input.IsDown(GameKey.A))
            move -= right;

        if (input.IsDown(GameKey.E))
            move += Vector3.UnitY;

        if (input.IsDown(GameKey.Q))
            move -= Vector3.UnitY;

        if (move.LengthSquared() > 1e-8f)
        {
            var speed = Speed * (input.Shift ? SprintMultiplier : 1f);
            position += Vector3.Normalize(move) * (speed * time.Delta);
            changed = true;
        }

        if (!changed)
            return;

        transform.TrySetWorldPose(world, position, rotation);
        world.SetComponent(self, transform);
    }
}
