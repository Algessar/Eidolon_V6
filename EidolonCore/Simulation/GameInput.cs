using System.Numerics;

namespace EidolonCore.Simulation;

/// <summary>Keys gameplay can read, sampled by the host once per frame.</summary>
/// <remarks>
/// A short, explicit list rather than a full keyboard map: gameplay only ever asks about a handful of
/// controls, and naming them keeps the engine out of the platform's key codes. Every value is a single
/// flag, combined with bitwise or.
/// </remarks>
[Flags]
public enum GameKey : ushort
{
    None = 0,

    W = 1 << 0,
    A = 1 << 1,
    S = 1 << 2,
    D = 1 << 3,
    Q = 1 << 4,
    E = 1 << 5,

    Space = 1 << 6,
    Shift = 1 << 7,
    Control = 1 << 8,

    Left = 1 << 9,
    Right = 1 << 10,
    Up = 1 << 11,
    Down = 1 << 12,
}

/// <summary>
/// One frame of gameplay input, sampled by the host and handed to the session.
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
    GameKey Keys)
{
    /// <summary>No input; also what the session holds when the game view is not focused.</summary>
    public static GameInput None => default;

    /// <summary>Sprint modifier: it changes the controls rather than being one.</summary>
    public bool Shift => IsDown(GameKey.Shift);

    /// <summary>Secondary modifier: it changes the controls rather than being one.</summary>
    public bool Control => IsDown(GameKey.Control);

    public bool IsDown(GameKey key) => (Keys & key) != 0;
}
