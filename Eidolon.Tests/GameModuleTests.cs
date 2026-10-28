using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
using EidolonGame;

namespace Eidolon.Tests;

public class GameModuleTests
{
    private const float Dt = 1f / 60f;

    private static (World World, SceneSession Session) Load()
    {
        var world = new World();
        var session = new SceneSession(world);

        new GameModule().Load(new GameContext(world, new AssetLibrary(), session));

        return (world, session);
    }

    [Fact]
    public void LoadPopulatesTheAuthoredWorld()
    {
        var (world, _) = Load();

        Assert.True(world.EntityCount > 5, $"expected scene content, found {world.EntityCount} entities");

        // Both views have their own camera, so gameplay can fly one without moving the other.
        Assert.Contains(world.GetEntitiesWith<Camera>(),
            entity => world.GetComponent<Camera>(entity).Kind == CameraKind.Editor);
        Assert.Contains(world.GetEntitiesWith<Camera>(),
            entity => world.GetComponent<Camera>(entity).Kind == CameraKind.Game);

        Assert.Contains(world.GetEntitiesWith<XpbdBody>(), entity => world.GetComponent<XpbdBody>(entity).InverseMass == 0f);
        Assert.Contains(world.GetEntitiesWith<XpbdBody>(), entity => world.GetComponent<XpbdBody>(entity).InverseMass > 0f);
    }

    [Fact]
    public void LoadRegistersTheGamesSystems()
    {
        var (_, session) = Load();

        Assert.Equal(3, session.Systems.Count);
        Assert.Contains(session.Systems, system => system is XpbdWorldSystem);
        Assert.Contains(session.Systems, system => system is SpinSystem);
        Assert.Contains(session.Systems, system => system is ScriptHost host && host.ScriptCount == 1);
    }

    [Fact]
    public void PlayModeFliesTheGameCameraButNotTheEditorCamera()
    {
        var (world, session) = Load();

        var gameCamera = world.GetEntitiesWith<Camera>()
            .First(entity => world.GetComponent<Camera>(entity).Kind == CameraKind.Game);
        var editorCamera = world.GetEntitiesWith<Camera>()
            .First(entity => world.GetComponent<Camera>(entity).Kind == CameraKind.Editor);

        var startOfGame = world.GetComponent<Transform>(gameCamera).GetWorldPosition(world);
        var startOfEditor = world.GetComponent<Transform>(editorCamera).GetWorldPosition(world);

        session.EnterPlay(world.Clone());
        session.SetInput(new GameInput(Vector2.Zero, Vector2.Zero, 0f, false, false, false, GameKey.W));
        session.Tick(Dt);

        var runtime = session.Runtime!;
        var movedGame = runtime.GetComponent<Transform>(gameCamera).GetWorldPosition(runtime);
        var movedEditor = runtime.GetComponent<Transform>(editorCamera).GetWorldPosition(runtime);

        Assert.True(Vector3.Distance(startOfGame, movedGame) > 0.01f, "the game camera did not move");
        VectorAssert.Near(startOfEditor, movedEditor);

        // And the authored world is untouched, which is what stopping play mode falls back to.
        VectorAssert.Near(startOfGame, world.GetComponent<Transform>(gameCamera).GetWorldPosition(world));
    }
}
