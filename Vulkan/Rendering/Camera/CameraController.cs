using System.Numerics;
using EidolonCore.Math;
using ImGuiNET;
using Silk.NET.Input;

namespace Eidolon.Vulkan;

//NOTE: This class is now the same as in an older working version.
internal class CameraController
{
    private Camera? _camera;
    private float _moveSpeed = 10.0f;
    private float _lookSensitivity = 0.07f;

    private readonly IInputContext _input;

    private Vector2 _lastMousePos;
    private float _yaw = -90;
    private float _pitch = 0f;

    public CameraController(IInputContext input, Camera? camera)
    {
        _input = input;
        _camera = camera ?? new Camera();

        foreach (var mouse in _input.Mice)
        {
            mouse.MouseMove += OnMouseMove;
        }
    }
    public void Update(double dt)
    {
        var keyboard = _input.Keyboards[0];
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
            _camera.Position = new Vector3(0, 0, 10);
            _camera.Target = new Vector3(0, 0, 0);
        }
    }

    
    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        var lookDelta = position - _lastMousePos;
        _lastMousePos = position;

        _yaw += lookDelta.X * _lookSensitivity;
        _pitch -= lookDelta.Y * _lookSensitivity;
        _pitch = Math.Clamp(_pitch, -89f, 89f);

        // Update camera target based on rotation
        Vector3 direction;
        direction.X = MathF.Cos(Mathf.DegreesToRadians(_yaw)) * MathF.Cos(Mathf.DegreesToRadians(_pitch));
        direction.Y = MathF.Sin(Mathf.DegreesToRadians(_pitch));
        direction.Z = MathF.Sin(Mathf.DegreesToRadians(_yaw)) * MathF.Cos(Mathf.DegreesToRadians(_pitch));
            
        _camera?.Target = _camera.Position + Vector3.Normalize(direction);
    }
}