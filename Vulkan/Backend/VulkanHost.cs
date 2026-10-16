using EidolonCore.Rendering;

namespace Eidolon.Vulkan;


public static class VulkanHost
{
    public static void Run(CompiledRenderGraph? initialGraph = null)
    {
       var master = new VulkanMaster(initialGraph);

       // if (!master.BackendIsRunning)
       // {
       //     master.Dispose();
       // }
    }
}