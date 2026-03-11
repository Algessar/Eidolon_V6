using System.ComponentModel;
using System.Numerics;
using Eidolon.Engine;
using Eidolon.Vulkan;

namespace EidolonEngine;

public sealed class Scene
{
    private readonly List<IComponent> _instances = new();

    public IReadOnlyList<IComponent> Instances => _instances;

    public void Add(IComponent instance) => _instances.Add(instance);
    public void Clear() => _instances.Clear();

    public void Update()
    {
        foreach (var obj in _instances)
        {
            
        }
    }
}
