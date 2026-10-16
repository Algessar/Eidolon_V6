using System.Numerics;
using EidolonCore.Math;
using ImGuiNET;
using Silk.NET.Input;

namespace Eidolon.Vulkan;

//NOTE: This class is now the same as in an older working version.
internal class CameraController
{
    private Camera? _camera;
    private float _moveSpeed = 5.0f;
    private float _lookSensitivity = 0.12f;

    private readonly IInputContext? _input;

    private Vector2 _lastMousePos;
    private bool _isLooking;
    private float _yaw = -90;
    private float _pitch = 0f;

    // Where the camera started, so it can be sent back home.
    private readonly Vector3 _homePosition;
    private readonly Vector3 _homeTarget;

    public CameraController(IInputContext? input, Camera? camera)
    {
        _input = input;
        _camera = camera ?? new Camera();

        _homePosition = _camera.Position;
        _homeTarget = _camera.Target;

        if (_input is not null)
        {
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseMove += OnMouseMove;
            }
        }

        // Start yaw/pitch from wherever the camera is already looking so the first
        // look-drag doesn't snap the view.
        var direction = Vector3.Normalize(_camera.Target - _camera.Position);
        _pitch = MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)) * Mathf.Rad2Deg;
        _yaw = MathF.Atan2(direction.Z, direction.X) * Mathf.Rad2Deg;
    }

    public void Update(double dt)
    {
        if (_camera is null || _input is null)
            return;

        // Hold the right mouse button to look around. Nothing else in the editor uses it,
        // so this keeps camera control from fighting the ImGui panels.
        var mouse = _input.Mice.FirstOrDefault();
        var wasLooking = _isLooking;
        _isLooking = mouse is not null && mouse.IsButtonPressed(MouseButton.Right);
        if (_isLooking && !wasLooking && mouse is not null)
        {
            // Don't jump when the look drag starts.
            _lastMousePos = mouse.Position;
        }

        var keyboard = _input.Keyboards.FirstOrDefault();
        if (keyboard is null || ImGui.GetIO().WantTextInput)
            return;

        float distance = _moveSpeed * (float)dt;

        // Simple WASD
        Vector3 forward = Vector3.Normalize(_camera.Target - _camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, _camera.Up));

        if (keyboard.IsKeyPressed(Key.W)) _camera.Move(forward * distance);
        if (keyboard.IsKeyPressed(Key.S)) _camera.Move(-forward * distance);
        if (keyboard.IsKeyPressed(Key.A)) _camera.Move(-right * distance);
        if (keyboard.IsKeyPressed(Key.D)) _camera.Move(right * distance);
        if (keyboard.IsKeyPressed(Key.E)) _camera.Move(Vector3.UnitY * distance);
        if (keyboard.IsKeyPressed(Key.Q)) _camera.Move(-Vector3.UnitY * distance);
        if (keyboard.IsKeyPressed(Key.Space))
        {
            _camera.Position = _homePosition;
            _camera.Target = _homeTarget;
        }
    }

    
    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        var lookDelta = position - _lastMousePos;
        _lastMousePos = position;

        if (!_isLooking || _camera is null)
            return;

        _yaw += lookDelta.X * _lookSensitivity;
        _pitch -= lookDelta.Y * _lookSensitivity;
        _pitch = Math.Clamp(_pitch, -89f, 89f);

        // Update camera target based on rotation
        Vector3 direction;
        direction.X = MathF.Cos(Mathf.DegreesToRadians(_yaw)) * MathF.Cos(Mathf.DegreesToRadians(_pitch));
        direction.Y = MathF.Sin(Mathf.DegreesToRadians(_pitch));
        direction.Z = MathF.Sin(Mathf.DegreesToRadians(_yaw)) * MathF.Cos(Mathf.DegreesToRadians(_pitch));
            
        _camera.Target = _camera.Position + Vector3.Normalize(direction);
    }
}
