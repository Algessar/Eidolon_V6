using EidolonCore.Nexus;
using EidolonCore.Simulation;

namespace EidolonGame;

/// <summary>
/// The sandbox: content and systems the editor loads as a game.
/// </summary>
/// <remarks>
/// This is the only type the host needs to know. Everything below it - the scene, the scripts, the
/// systems - is game code that the engine has never heard of.
/// </remarks>
public sealed class GameModule : IGameModule
{
    public void Load(GameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var player = DefaultScene.Populate(context.World, context.Assets);

        var scripts = new ScriptHost(context.Session);
        scripts.Add(player, new PlayerCameraController());

        // Scripts run before the physics step, so what a script writes is what gets simulated.
        context.Session.AddSystem(scripts);
        context.Session.AddSystem(new SpinSystem());
        context.Session.AddSystem(new XpbdWorldSystem());
    }
}
