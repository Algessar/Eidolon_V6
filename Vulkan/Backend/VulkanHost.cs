using EidolonCore.Assets;
using EidolonCore.ECS;

namespace Eidolon.Vulkan;

public static class VulkanHost
{
    public static void Run(World world, AssetLibrary assets, CompiledRenderGraph? initialGraph = null)
    {
        // The constructor runs the window loop, so disposal happens once it returns
        // (either because the window was closed or because startup threw).
        using var master = new VulkanMaster(world, assets, initialGraph);
    }
}
