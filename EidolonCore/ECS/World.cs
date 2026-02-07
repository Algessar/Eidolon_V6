namespace EidolonCore.ECS;
public class World
{
    private uint _nextId = 1;

    private Dictionary<Entity, Dictionary<Type, IComponent>> _entities = new();

    public Entity CreateEntity()
    {
        var entity = new Entity(_nextId++);
        _entities[entity] = new Dictionary<Type, IComponent>();
        return entity;
    }

    public void AddComponent<T>(Entity entity, T component) where T : IComponent
    {
        if (_entities.TryGetValue(entity, out var components))
        {
            components[typeof(T)] = component;
        }
    }

    public T GetComponent<T>(Entity entity) where T : IComponent
    {
        if (_entities.TryGetValue(entity, out var components) &&
            components.TryGetValue(typeof(T), out var component))
        {
            return (T)component;
        }
        throw new Exception($"Entity {entity.Id} doesn't have component {typeof(T).Name}");
    }

    public bool HasComponent<T>(Entity entity) where T : IComponent
    {
        return _entities.TryGetValue(entity, out var components) && 
               components.ContainsKey(typeof(T));
    }

    public IEnumerable<Entity> GetEntitiesWith<T>() where T : IComponent
    {
        foreach (var kvp in _entities)
        {
            if (kvp.Value.ContainsKey(typeof(T)))
            {
                yield return kvp.Key;
            }
        }
    }

    public override string ToString()
    {
        return "Entity ID" + _nextId;
    }
}