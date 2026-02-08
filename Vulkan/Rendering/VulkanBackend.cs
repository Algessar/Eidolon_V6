
namespace Eidolon.Vulkan;

internal class VulkanBackend
{
	static VulkanMaster _vulkanMaster;
	public static void Main()
	{
		Debug.Log("VulkanRenderer called");

		_vulkanMaster = new VulkanMaster();
	}
}
