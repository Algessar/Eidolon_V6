using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Eidolon.Editor;

internal class ImGuiInputManager
{
    
    IInputContext _input;
    IKeyboard? _keyboard;
    IMouse? _mouse;
    
    private readonly List<char> _textInput = [];
    
    IWindow _window;
    
    public ImGuiInputManager(IWindow window)
    {
        _window = window;
        _input = _window.CreateInput();
        SetInput();
    }

    private void SetInput()
    {
        _keyboard = _input.Keyboards.FirstOrDefault();
        _mouse = _input.Mice.FirstOrDefault();

        if (_keyboard != null)
            _keyboard.KeyChar += (_, c) => _textInput.Add(c);
    }
    
    public void UpdateInput(ImGuiIOPtr io)
    {
        if (_input == null ) 
        {
            // Clear input when window not focused
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
            for (int i = 0; i < 5; i++)
                io.AddMouseButtonEvent(i, false);
            return;
        }
    
        UpdateMouseEvents(io);
        UpdateKeyEvents(io);
    }
    
    private void UpdateMouseEvents(ImGuiIOPtr io)
    {
        if (_mouse != null)
        {
            io.AddMousePosEvent(_mouse.Position.X, _mouse.Position.Y);
            io.AddMouseButtonEvent(0, _mouse.IsButtonPressed(MouseButton.Left));
            io.AddMouseButtonEvent(1, _mouse.IsButtonPressed(MouseButton.Right));
            io.AddMouseButtonEvent(2, _mouse.IsButtonPressed(MouseButton.Middle));
            if (_mouse.ScrollWheels.Count > 0)
            {
                var wheel = _mouse.ScrollWheels[0];
                io.AddMouseWheelEvent(wheel.X, wheel.Y);
            }
        }
    }
    
    private void UpdateKeyEvents(ImGuiIOPtr io)
    {
        if (_keyboard != null)
        {
            // Process text input
            ProcessTextInput(io);

            // Modifier keys
            io.AddKeyEvent(ImGuiKey.ModCtrl,
                _keyboard.IsKeyPressed(Key.ControlLeft) ||
                _keyboard.IsKeyPressed(Key.ControlRight));

            io.AddKeyEvent(ImGuiKey.ModShift,
                _keyboard.IsKeyPressed(Key.ShiftLeft) ||
                _keyboard.IsKeyPressed(Key.ShiftRight));

            io.AddKeyEvent(ImGuiKey.ModAlt,
                _keyboard.IsKeyPressed(Key.AltLeft) ||
                _keyboard.IsKeyPressed(Key.AltRight));

            io.AddKeyEvent(ImGuiKey.ModSuper,
                _keyboard.IsKeyPressed(Key.SuperLeft) ||
                _keyboard.IsKeyPressed(Key.SuperRight));

            // Individual keys
            KeyMapping(io);
        }
    }
    
    private void ProcessTextInput(ImGuiIOPtr io)
    {
        if (_textInput.Count == 0) return;
            
        // Use AddInputCharactersUTF8 for bulk text
        foreach (var ch in _textInput)
        {
            // Use UTF16 version for char input
            io.AddInputCharacterUTF16(ch);
        }
            
        _textInput.Clear();
    }
    
    private void KeyMapping(ImGuiIOPtr io)
    {
        // Map Silk.NET keys to ImGui keys
        var keyMap = new Dictionary<Key, ImGuiKey>
        {
            // Navigation
            { Key.Tab, ImGuiKey.Tab },
            { Key.Left, ImGuiKey.LeftArrow },
            { Key.Right, ImGuiKey.RightArrow },
            { Key.Up, ImGuiKey.UpArrow },
            { Key.Down, ImGuiKey.DownArrow },
            { Key.PageUp, ImGuiKey.PageUp },
            { Key.PageDown, ImGuiKey.PageDown },
            { Key.Home, ImGuiKey.Home },
            { Key.End, ImGuiKey.End },
            { Key.Insert, ImGuiKey.Insert },
            { Key.Delete, ImGuiKey.Delete },
            { Key.Backspace, ImGuiKey.Backspace },
            { Key.Space, ImGuiKey.Space },
            { Key.Enter, ImGuiKey.Enter },
            { Key.Escape, ImGuiKey.Escape },
            { Key.KeypadEnter, ImGuiKey.KeypadEnter },
                
            // Letters
            { Key.A, ImGuiKey.A },
            { Key.C, ImGuiKey.C },
            { Key.V, ImGuiKey.V },
            { Key.X, ImGuiKey.X },
            { Key.Y, ImGuiKey.Y },
            { Key.Z, ImGuiKey.Z },
                
            // Numbers
            { Key.Number0, ImGuiKey._0 },
            { Key.Number1, ImGuiKey._1 },
            { Key.Number2, ImGuiKey._2 },
            { Key.Number3, ImGuiKey._3 },
            { Key.Number4, ImGuiKey._4 },
            { Key.Number5, ImGuiKey._5 },
            { Key.Number6, ImGuiKey._6 },
            { Key.Number7, ImGuiKey._7 },
            { Key.Number8, ImGuiKey._8 },
            { Key.Number9, ImGuiKey._9 },
                
            // Function keys
            { Key.F1, ImGuiKey.F1 },
            { Key.F2, ImGuiKey.F2 },
            { Key.F3, ImGuiKey.F3 },
            { Key.F4, ImGuiKey.F4 },
            { Key.F5, ImGuiKey.F5 },
            { Key.F6, ImGuiKey.F6 },
            { Key.F7, ImGuiKey.F7 },
            { Key.F8, ImGuiKey.F8 },
            { Key.F9, ImGuiKey.F9 },
            { Key.F10, ImGuiKey.F10 },
            { Key.F11, ImGuiKey.F11 },
            { Key.F12, ImGuiKey.F12 },
        };
            
        // Update each key
        foreach (var mapping in keyMap)
        {
            var silkKey = mapping.Key;
            var imguiKey = mapping.Value;
                
            bool isPressed = _keyboard.IsKeyPressed(silkKey);
            io.AddKeyEvent(imguiKey, isPressed);
        }
    }
}