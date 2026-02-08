using Eidolon.Vulkan;
namespace Eidolon.Editor;

public class EidolonEditor
{
    public static void Main()
    {
        Debug.Log("Starting from EidolonEditor", VALIDATION_LAYERS.INFO);
        VulkanHost.Run();
        Debug.Log("EidolonEditor shutting down!", VALIDATION_LAYERS.SUCCESS);
    }
}