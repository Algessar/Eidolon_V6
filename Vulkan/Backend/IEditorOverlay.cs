using System.Numerics;

namespace Eidolon.Vulkan;

/// <summary>The editor UI as the renderer sees it: view sizes and the textures to show, nothing more.</summary>
/// <remarks>
/// The renderer drives one ImGui frame per frame but does not own the panels. The host supplies an
/// implementation, so the renderer carries no editor code and the editor can live in its own assembly.
/// <see cref="Update"/> is called with the ImGui frame current.
/// </remarks>
public interface IEditorOverlay
{
    /// <summary>Pixel size of the scene viewport image, as last laid out.</summary>
    Vector2 EditorViewSize { get; }

    /// <summary>Pixel size of the game view image, as last laid out.</summary>
    Vector2 GameViewSize { get; }

    /// <summary>
    /// Called once, after the backend has created the ImGui context, to let the overlay configure it.
    /// The host builds the overlay before the backend starts, so nothing here may touch ImGui earlier.
    /// </summary>
    void Initialize();

    /// <summary>Hands the overlay the texture ids the renderer rendered this frame, or zero for none.</summary>
    void SetViewTextures(nint editorViewTextureId, nint gameViewTextureId);

    /// <summary>Draws the editor for one frame.</summary>
    void Update();
}
