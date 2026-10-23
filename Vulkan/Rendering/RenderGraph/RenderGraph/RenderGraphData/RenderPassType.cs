namespace Eidolon.Vulkan;

public enum RenderPassType
{
    Geometry,
    Shadow,
    Lighting,
    PostProcess,
    UI,
    GameView,

    /// <summary>The editor viewport: same scene, driven by the editor camera.</summary>
    EditorView,
    Present
}
