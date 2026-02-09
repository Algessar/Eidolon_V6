using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

public static class VulkanHost
{
    public static void Run(CompiledRenderGraph? initialGraph = null)
    {
        _ = new VulkanMaster(initialGraph);
    }
}

internal class InternalAccess : IInternalAccess
{
    private uint someField;
    public uint Foonction() => someField;
}

public interface IInternalAccess
{
}

public class PublicAccess
{
    public IInternalAccess Foo { get; } = new InternalAccess();
}