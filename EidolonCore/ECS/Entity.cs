namespace EidolonCore.ECS;

/// <summary>
/// A handle to an entity inside a <see cref="World"/>. It is just an id: the entity owns no
/// data itself, components do.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public readonly uint Id;

    public Entity(uint id) => Id = id;

    /// <summary>False for the default value, which is not a real entity.</summary>
    public bool IsValid => Id != 0;

    public bool Equals(Entity other) => Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => (int)Id;

    public override string ToString() => $"Entity({Id})";

    public static bool operator ==(Entity left, Entity right) => left.Id == right.Id;

    public static bool operator !=(Entity left, Entity right) => left.Id != right.Id;
}
