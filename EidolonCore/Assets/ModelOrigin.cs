using EidolonCore.ECS;

namespace EidolonCore.Assets;

/// <summary>
/// Where an entity came from when it was spawned from an imported file.
/// </summary>
/// <remarks>
/// <para>
/// A scene records the source file once, on the entity the model was spawned on, plus each entity's
/// node index. Loading imports the file again and rebuilds the same entities with the same ids, which
/// keeps parent links and bone references intact. The geometry itself is never written into the scene:
/// the file is the asset.
/// </para>
/// <para>
/// An entity without this component is authored in the scene, not imported, and is saved in full.
/// </para>
/// </remarks>
public readonly struct ModelOrigin : IComponent
{
    /// <summary>The entity the file was spawned on. Every node of an instance points at it.</summary>
    public Entity Root { get; init; }

    /// <summary>The model file. Only the instance's root records it.</summary>
    public string? SourcePath { get; init; }

    /// <summary>This entity's node index in the file, which is how a load finds it again.</summary>
    public int NodeIndex { get; init; }
}
