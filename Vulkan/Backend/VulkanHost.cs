namespace Eidolon.Vulkan;

public static class VulkanHost
{
    public static void Run(CompiledRenderGraph? initialGraph = null)
    {
        new VulkanMaster(initialGraph);
    }
}
