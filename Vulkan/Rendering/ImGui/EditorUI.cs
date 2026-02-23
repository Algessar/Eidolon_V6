using System.Numerics;
using Eidolon.Vulkan;
using ImGuiNET;
using Silk.NET.Windowing;

namespace Eidolon.Editor;

internal class EditorUI
{
    private readonly IWindow _window;
    
    public EditorUI(IWindow window)
    {
        _window = window;
        InitialSetup();

    }
    
    void InitialSetup()
    {
        var io = ImGui.GetIO();
        
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        
        
        // io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/Arial.ttf", 16.0f);
        
        float dpiScale = 0.99f;
        io.DisplaySize = new Vector2(_window.Size.X, _window.Size.Y);
        
        // Framebuffer scaling (HiDPI)
        io.DisplayFramebufferScale = new Vector2(
            _window.FramebufferSize.X / (float)_window.Size.X,
            _window.FramebufferSize.Y / (float)_window.Size.Y
        );  
        
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();

        if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        style.ScaleAllSizes(dpiScale);
    }
    
    public void Update()
    {
        DrawDockSpace();
    }
    
    private void DrawDockSpace()
    {
        var io = ImGui.GetIO();
        var viewport = ImGui.GetMainViewport();
        
        // Use WorkPos / WorkSize instead of DisplaySize
        ImGui.SetNextWindowPos(viewport.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.WorkSize, ImGuiCond.Always);
        ImGui.SetNextWindowViewport(viewport.ID);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        ImGui.Begin(
            "##DockSpaceRoot",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.AlwaysAutoResize 
            // | ImGuiWindowFlags.NoDecoration 
            //| ImGuiWindowFlags.MenuBar
            
        );
        ImGui.PopStyleVar(3);
        
        uint dockspaceId = ImGui.GetID("MainDockSpace");
        ImGui.DockSpace(dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
        ImGui.End();
    }
}

internal struct DockedWindow
{
    public string Name;
    public Vector2 Size;
    public bool Collapsed;
    public ImGuiWindowFlags Flags;

    public Vector2 WindowPos;
    public ImGuiCond WindowCond;

    // Unified way to create docked windows
    /*
     * What is my intent here? I want a way to quickly create windows:
     * - Animation window
     * - Inspector
     * - Hierarchy
     *
     * One reason being that after closing a window, it should be easy to create a new one of that type.
     * 
     * 
     */

    public void Update()
    {
        
        //ImGui.Begin() -> flags
        
        // Condition
        
        // Begin menu
        // Begin Item
        
        // ImGui.End();
        
    }
}

public enum WindowType
{
    INSPECTOR,
    EDITOR,
    VIEW,
    
}