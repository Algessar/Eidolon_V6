

using Eidolon.Vulkan;
using EidolonCore.ECS;

namespace EidolonEngine;

public sealed class Scene
{

    public bool CurrentScene;
    
    private readonly List<Component> _instances = new();

    public IReadOnlyList<Component> Instances => _instances;

    public void Add(Component instance) => _instances.Add(instance);
    public void Clear() => _instances.Clear();

    public void Update()
    {
        //iterates through each component in the scene and updates them
        foreach (var obj in _instances)
        {
            obj.Update();
        }
    }
}
