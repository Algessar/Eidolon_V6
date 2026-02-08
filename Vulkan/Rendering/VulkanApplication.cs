
namespace Eidolon.Vulkan;

internal class VulkanApplication
{
	static VulkanMaster _vulkanMaster;
	public static void Main()
	{
		Debug.Log("VulkanRenderer called");

		_vulkanMaster = new VulkanMaster();
	}
}
