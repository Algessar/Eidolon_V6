using EidolonCore.Assets;
using EidolonCore.ECS;

namespace EidolonCore.Simulation;

/// <summary>What a game is handed when the host starts it.</summary>
/// <remarks>
/// The host builds one session per run and lets the game fill in the authored world and register its
/// systems. Everything the game touches comes through this object, which is what lets the engine and
/// the editor stay unaware of any particular game.
/// </remarks>
public sealed class GameContext
{
    public GameContext(World world, AssetLibrary assets, SceneSession session)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>The authored world. A game creates its content here, not in the runtime copy.</summary>
    public World World { get; }

    /// <summary>The asset library the host discovered, so a game can load meshes by name.</summary>
    public AssetLibrary Assets { get; }

    /// <summary>The session, used to register gameplay systems.</summary>
    public SceneSession Session { get; }
}

/// <summary>
/// A game: content plus the systems that drive it, loaded once by the host.
/// </summary>
/// <remarks>
/// Implementing this is the whole contract between a game and the engine. A game assembly references
/// the engine; the engine never references a game, and the editor only knows this interface.
/// </remarks>
public interface IGameModule
{
    /// <summary>Populates <see cref="GameContext.World"/> and registers systems on the session.</summary>
    void Load(GameContext context);
}
