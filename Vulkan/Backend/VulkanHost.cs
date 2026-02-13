using EidolonCore.Rendering;

namespace Eidolon.Vulkan;


public static class VulkanHost
{
    public static void Run(CompiledRenderGraph? initialGraph = null)
    {
        _ = new VulkanMaster(initialGraph);
    }
    
    
}