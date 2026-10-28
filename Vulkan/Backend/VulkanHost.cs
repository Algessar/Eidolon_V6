using EidolonCore.Assets;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

/// <summary>Runs the backend until the window closes.</summary>
/// <remarks>
/// The host owns the window, the viewport settings and the editor overlay; the backend only drives the
/// loop. That keeps window creation, editor panels and rendering in separate hands.
/// </remarks>
public static class VulkanHost
{
    public static void Run(IWindow window, SceneSession session, AssetLibrary assets, ViewportSettings viewport,
        IEditorOverlay editor, CompiledRenderGraph? initialGraph = null)
    {
        // The constructor runs the window loop, so disposal happens once it returns
        // (either because the window was closed or because startup threw).
        using var master = new VulkanMaster(window, session, assets, viewport, editor, initialGraph);
    }
}
