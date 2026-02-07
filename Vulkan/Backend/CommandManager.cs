using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class CommandManager(VulkanManager vulkanManager)
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?
    
    [Group("References")]
    VulkanManager _vulkanManager = vulkanManager;
    
    [Group("Resources")]
    public CommandPool CommandPool { get; set; }
    
}