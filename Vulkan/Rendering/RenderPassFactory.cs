namespace Eidolon.Vulkan.Rendering;

internal unsafe class RenderPassFactory(VulkanMaster vulkanMaster)
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?
    VulkanMaster _vulkanMaster = vulkanMaster;
}