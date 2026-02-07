using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class BufferFactory(VulkanManager vulkanManager)
{
    // Center for creating buffers (all types?)
    VulkanManager _vulkanManager = vulkanManager;


    public GpuBuffer CreateGpuBuffer()
    {
        return new GpuBuffer();
    }

    //SET FLAGS ETC
    public Framebuffer CreateFramebuffer()
    {
        
        
        return new Framebuffer();
    }
     
}