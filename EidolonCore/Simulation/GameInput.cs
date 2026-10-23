using System.Numerics;

namespace EidolonCore.Simulation;

/// <summary>
/// One frame of gameplay input, sampled by the editor and handed to the session.
/// </summary>
/// <remarks>
/// Only populated while play mode runs and the game view has the cursor, so gameplay never sees
/// input that was meant for an editor panel, and editor shortcuts (which only fire over the scene
/// viewport) can never collide with it.
/// </remarks>
public readonly record struct GameInput(
    Vector2 MousePosition,
    Vector2 MouseDelta,
    float Scroll,
    bool LeftDown,
    bool RightDown,
    bool MiddleDown,
    bool Shift,
    bool Control)
{
    /// <summary>No input; also what the session holds when the game view is not focused.</summary>
    public static GameInput None => default;
}
