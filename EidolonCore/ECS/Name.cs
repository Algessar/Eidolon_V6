namespace EidolonCore.ECS;

/// <summary>
/// A stable, human-readable name for an entity, used to address it by hierarchy path.
/// </summary>
/// <remarks>
/// Names are only as unique as the code that creates them: when two siblings share one, path lookup
/// takes the first match. Clips bind by path rather than by entity id precisely so they stay readable
/// and reusable, and so an importer has something to map bone names onto.
/// </remarks>
public readonly struct Name : IComponent
{
    public Name(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
