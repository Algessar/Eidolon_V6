using EidolonCore.ECS;

namespace EidolonCore.Rendering;

/// <summary>Marks an entity to be drawn with a selection outline.</summary>
/// <remarks>
/// A marker rather than a renderer call: the editor owns selection, the renderer owns drawing, and this
/// is the only thing they share. It also copies with play mode for free, because it is a value type.
/// </remarks>
public readonly struct Highlight : IComponent
{
}
