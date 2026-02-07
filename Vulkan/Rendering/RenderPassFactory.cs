namespace Eidolon.Vulkan;

internal unsafe class RenderPassFactory(VulkanManager vulkanManager)
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?
    VulkanManager _vulkanManager = vulkanManager;
}