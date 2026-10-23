using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EidolonCore.ECS;

/// <summary>
/// Stores entities and their components. A world is the engine-core scene representation and
/// knows nothing about rendering, physics or any other backend.
/// </summary>
/// <remarks>
/// <para>
/// Each component type gets its own table, so lookups do not box and <see cref="GetComponent{T}"/>
/// can hand out a writable reference into the table.
/// </para>
/// <para>
/// As with <see cref="Dictionary{TKey,TValue}"/>, a reference returned by
/// <see cref="GetComponent{T}"/> is invalidated by any call that adds or removes components.
/// </para>
/// <para>
/// Enumeration is lazy over the underlying tables, so mutating the world while iterating the
/// result of <see cref="GetEntitiesWith{T}"/> throws.
/// </para>
/// </remarks>
public class World
{
    private uint _nextId = 1;
    private readonly HashSet<Entity> _entities = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    public IReadOnlyCollection<Entity> Entities => _entities;

    public int EntityCount => _entities.Count;

    public Entity CreateEntity()
    {
        var entity = new Entity(_nextId++);
        _entities.Add(entity);
        return entity;
    }

    public bool IsAlive(Entity entity) => _entities.Contains(entity);

    /// <summary>
    /// Copies every entity and component into a new world, keeping entity ids so references
    /// (selection, parents) stay valid across the copy.
    /// </summary>
    /// <param name="copiers">
    /// Per-component-type copy functions. Components without an entry are copied by reference,
    /// which is correct for value types and for shared immutable data (meshes), but not for
    /// mutable classes - register those here.
    /// </param>
    public World Clone(IReadOnlyDictionary<Type, Func<IComponent, IComponent>>? copiers = null)
    {
        var clone = new World
        {
            _nextId = _nextId,
        };

        foreach (var entity in _entities)
            clone._entities.Add(entity);

        foreach (var store in _stores.Values)
            store.CopyInto(clone, copiers);

        return clone;
    }

    /// <summary>Removes the entity and every component attached to it.</summary>
    /// <remarks>
    /// Children are orphaned, but their world transform is preserved: destroying a parent does not
    /// teleport everything that was under it.
    /// </remarks>
    public bool DestroyEntity(Entity entity)
    {
        if (!_entities.Remove(entity))
            return false;

        // World matrices have to be captured while the doomed transform is still around.
        List<(Entity Entity, Matrix4x4 Matrix)>? orphans = null;

        if (TryGetStore<Transform>(out var transformsBefore))
        {
            foreach (var other in transformsBefore.Entities)
            {
                if (transformsBefore.TryGet(other, out var transform) && transform.Parent == entity)
                    (orphans ??= []).Add((other, transform.GetWorldMatrix(this)));
            }
        }

        foreach (var store in _stores.Values)
            store.Remove(entity);

        if (orphans is not null && TryGetStore<Transform>(out var transforms))
        {
            foreach (var (orphan, worldMatrix) in orphans)
            {
                if (!transforms.TryGet(orphan, out var transform))
                    continue;

                if (Matrix4x4.Decompose(worldMatrix, out var scale, out var rotation, out var translation))
                {
                    transform.Position = translation;
                    transform.Rotation = rotation;
                    transform.Scale = scale;
                }

                transform.Parent = default;
                transforms.Set(orphan, transform);
            }
        }

        return true;
    }

    /// <summary>Adds the component, replacing any existing component of the same type.</summary>
    public void SetComponent<T>(Entity entity, T component) where T : IComponent
    {
        if (!_entities.Contains(entity))
            throw new InvalidOperationException($"Entity {entity.Id} does not exist in this world.");

        GetOrCreateStore<T>().Set(entity, component);
    }

    /// <exception cref="InvalidOperationException">The entity has no component of that type.</exception>
    public ref T GetComponent<T>(Entity entity) where T : IComponent
    {
        if (!TryGetStore<T>(out var store))
            throw new InvalidOperationException($"Entity {entity.Id} has no {typeof(T).Name} component.");

        ref var component = ref store.GetRefOrNullRef(entity);
        if (Unsafe.IsNullRef(ref component))
            throw new InvalidOperationException($"Entity {entity.Id} has no {typeof(T).Name} component.");

        return ref component!;
    }

    /// <summary>Reads the component by value; use <see cref="GetComponent{T}"/> to mutate in place.</summary>
    public bool TryGetComponent<T>(Entity entity, [MaybeNullWhen(false)] out T component) where T : IComponent
    {
        if (TryGetStore<T>(out var store) && store.TryGet(entity, out var value))
        {
            component = value;
            return true;
        }

        component = default!;
        return false;
    }

    public bool HasComponent<T>(Entity entity) where T : IComponent
        => TryGetStore<T>(out var store) && store.Has(entity);

    public bool RemoveComponent<T>(Entity entity) where T : IComponent
        => TryGetStore<T>(out var store) && store.Remove(entity);

    /// <summary>Every entity carrying a <typeparamref name="T"/> component.</summary>
    public IEnumerable<Entity> GetEntitiesWith<T>() where T : IComponent
    {
        if (!TryGetStore<T>(out var store))
            yield break;

        foreach (var entity in store.Entities)
            yield return entity;
    }

    /// <summary>Every entity carrying both component types.</summary>
    public IEnumerable<Entity> GetEntitiesWith<T1, T2>()
        where T1 : IComponent
        where T2 : IComponent
    {
        foreach (var entity in GetEntitiesWith<T1>())
        {
            if (HasComponent<T2>(entity))
                yield return entity;
        }
    }

    private ComponentStore<T> GetOrCreateStore<T>() where T : IComponent
    {
        if (_stores.TryGetValue(typeof(T), out var existing))
            return (ComponentStore<T>)existing;

        var store = new ComponentStore<T>();
        _stores[typeof(T)] = store;
        return store;
    }

    private bool TryGetStore<T>(out ComponentStore<T> store) where T : IComponent
    {
        if (_stores.TryGetValue(typeof(T), out var existing))
        {
            store = (ComponentStore<T>)existing;
            return true;
        }

        store = null!;
        return false;
    }

    private interface IComponentStore
    {
        IEnumerable<Entity> Entities { get; }

        bool Has(Entity entity);

        bool Remove(Entity entity);

        /// <summary>Copies every component of this type into <paramref name="target"/>.</summary>
        void CopyInto(World target, IReadOnlyDictionary<Type, Func<IComponent, IComponent>>? copiers);
    }

    private sealed class ComponentStore<T> : IComponentStore where T : IComponent
    {
        private readonly Dictionary<Entity, T> _components = new();

        public IEnumerable<Entity> Entities => _components.Keys;

        public bool Has(Entity entity) => _components.ContainsKey(entity);

        public bool Remove(Entity entity) => _components.Remove(entity);

        public void CopyInto(World target, IReadOnlyDictionary<Type, Func<IComponent, IComponent>>? copiers)
        {
            Func<IComponent, IComponent>? copier = null;
            copiers?.TryGetValue(typeof(T), out copier);

            var destination = target.GetOrCreateStore<T>();

            foreach (var (entity, component) in _components)
                destination._components[entity] = copier is null ? component : (T)copier(component);
        }

        public void Set(Entity entity, T component) => _components[entity] = component;

        public bool TryGet(Entity entity, out T component)
        {
            if (_components.TryGetValue(entity, out var value))
            {
                component = value;
                return true;
            }

            component = default!;
            return false;
        }

        /// <summary>Returns a null reference when the entity has no component of this type.</summary>
        public ref T GetRefOrNullRef(Entity entity)
            => ref CollectionsMarshal.GetValueRefOrNullRef(_components, entity)!;
    }
}
