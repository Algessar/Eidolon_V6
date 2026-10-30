using EidolonCore.ECS;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;

namespace EidolonCore.Simulation;

/// <summary>
/// Copies the authored world for play mode with the engine's component rules:
/// <see cref="Transform"/> is a value type and copies by itself, <see cref="MeshRenderer"/> keeps
/// the shared mesh but gets its own material, <see cref="Camera"/> is copied so the runtime camera
/// cannot move the authored one, and <see cref="AnimationPlayer"/> is copied so the playhead runs in
/// the snapshot rather than in the authored scene.
/// </summary>
public static class SceneCloner
{
    private static readonly IReadOnlyDictionary<Type, Func<IComponent, IComponent>> Copiers =
        new Dictionary<Type, Func<IComponent, IComponent>>
        {
            [typeof(MeshRenderer)] = component => new MeshRenderer((MeshRenderer)component),
            [typeof(Camera)] = component => new Camera((Camera)component),
            [typeof(AnimationPlayer)] = component => new AnimationPlayer((AnimationPlayer)component),
            [typeof(SkinnedMesh)] = component => new SkinnedMesh((SkinnedMesh)component),
        };

    public static World Clone(World authored) => authored.Clone(Copiers);
}
