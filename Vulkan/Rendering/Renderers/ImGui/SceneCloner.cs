using Eidolon.Vulkan;
using EidolonCore.ECS;

namespace Eidolon.Editor;

/// <summary>
/// Copies the authored world for play mode with the engine's component rules:
/// <see cref="Transform"/> is a value type and copies by itself, <see cref="MeshRenderer"/> keeps
/// the shared mesh but gets its own material, and <see cref="Camera"/> is copied so the runtime
/// camera cannot move the authored one.
/// </summary>
internal static class SceneCloner
{
    private static readonly IReadOnlyDictionary<Type, Func<IComponent, IComponent>> Copiers =
        new Dictionary<Type, Func<IComponent, IComponent>>
        {
            [typeof(MeshRenderer)] = component => new MeshRenderer((MeshRenderer)component),
            [typeof(Camera)] = component => new Camera((Camera)component),
        };

    public static World Clone(World authored) => authored.Clone(Copiers);
}
