using EidolonCore.ECS;

namespace Eidolon.Vulkan;

public class Component
{

    public Transform Transform = new();
    
    public virtual void Update()
    {
        
    }

    public virtual void NewFrame()
    {
        
    }
}