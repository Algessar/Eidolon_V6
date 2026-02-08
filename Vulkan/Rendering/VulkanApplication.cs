
using EidolonCore.Rendering;
using ImGuiNET;
using Silk.NET.Input.Glfw;

namespace Eidolon.Vulkan;

internal class VulkanApplication 
{
	static VulkanMaster _vulkanMaster;
	public static void Main()
	{
		Debug.Log("VulkanRenderer called");
		GlfwInput.RegisterPlatform();

		_vulkanMaster = new VulkanMaster();
	}

	public void Dispose()
	{
		// TODO release managed resources here
	}
}
