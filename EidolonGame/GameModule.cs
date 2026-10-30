using EidolonCore.Nexus;
using EidolonCore.Nexus.Animation;
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

        // Order is layering: gameplay scripts write intent, animation turns clips into desired poses,
        // the resolver commits them, and physics gets the last word on everything it simulates.
        // Skinning is not a system here: the renderer applies the bones in the vertex shader.
        context.Session.AddSystem(scripts);
        context.Session.AddSystem(new AnimationSystem());
        context.Session.AddSystem(new PoseResolutionSystem());
        context.Session.AddSystem(new SpinSystem());
        context.Session.AddSystem(new XpbdWorldSystem());
    }
}
