
using Silk.NET.Input;
using System.Numerics;
using EidolonCore.Math;
using EidolonEngine;
using ImGuiNET;

namespace Eidolon.Engine;

public class CameraController
{
    private readonly Camera _camera;
    public Camera Camera => _camera;
    private readonly IInputContext _input;
    private float _moveSpeed = 10.0f;
    private float _lookSensitivity = 0.07f;

    private Vec2 _lastMousePos;
    private float _yaw = -90f;
    private float _pitch = 0f;
    private bool _isMiddleMouseDown;
    private bool _isShiftDown = false;

    public CameraController(IInputContext input)
    {
        // _camera = camera;
        _camera =  new()
        {
            Position = new Vector3(0f, 5f, 5f),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
        };
        _input = input;
        
        // Listen for mouse movement
        foreach (var mouse in _input.Mice)
        {

            // mouse.CursorMode = CursorMode.Disabled; // Lock mouse to window
            mouse.MouseMove += OnMouseMove;
            mouse.MouseDown += (m, b) => HandleMouseButton(m, b, true);
            mouse.MouseUp += (m, b) => HandleMouseButton(m, b, false);
        }
    }

    public void Update(double dt)
    {
        var keyboard = _input.Keyboards[0];

        _isShiftDown = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);
        
        if(_isMiddleMouseDown)
        {
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

        }
        if (keyboard.IsKeyPressed(Key.Space))
        {
            _camera.Position = new Vector3(0, 5, 5);
            _camera.Target = new Vector3(0, 0, 0);
        }


        foreach (var mouse in _input.Mice)
        {
            if (keyboard.IsKeyPressed(Key.Escape))
				mouse.Cursor.CursorMode = CursorMode.Normal;
            if (mouse.IsButtonPressed(MouseButton.Left))
            {
	            mouse.Cursor.CursorMode = CursorMode.Disabled;
            }
        }
    }
    
    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        OnMouseMove(mouse, (Vec2)position);
    }
    
    private void OnMouseMove(IMouse mouse, Vec2 position)
    {
        if (!_isMiddleMouseDown)
        {
            _lastMousePos = position;
        }
        
        // if (ImGui.GetIO().WantCaptureMouse)
        //     return; // Don't rotate camera when interacting with ImGui
       
        Vec2 lookDelta = position - _lastMousePos;
        _lastMousePos = position;

        _yaw += lookDelta.x * _lookSensitivity;
        _pitch -= lookDelta.y * _lookSensitivity;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);

        // Update camera target based on rotation
        Vector3 direction;
        
        direction.X = Mathf.Cos(Mathf.DegreesToRadians(_yaw)) * Mathf.Cos(Mathf.DegreesToRadians(_pitch));
        direction.Y = Mathf.Sin(Mathf.DegreesToRadians(-_pitch));
        direction.Z = Mathf.Sin(Mathf.DegreesToRadians(_yaw)) * Mathf.Cos(Mathf.DegreesToRadians(_pitch));
        
        _camera.Target = _camera.Position + Vector3.Normalize(direction);
    }
    
    public void HandleMouseButton(IMouse mouse, MouseButton button, bool isDown)
    {
        if (button == MouseButton.Middle)
        {
            _isMiddleMouseDown = isDown;
            // Optional: Lock/unlock cursor when middle mouse is pressed
            mouse.Cursor.CursorMode = isDown ? CursorMode.Disabled : CursorMode.Normal;
        }
    }

    public void Dispose()
    {
        _input.Dispose();
    }
}

