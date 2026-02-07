using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal class ImGuiRenderer(VulkanManager vulkanManager) : IRenderer
{
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public Framebuffer Framebuffer { get; }
    public GpuBuffer GpuBuffer { get; }
    public GpuImage GpuImage { get; }
    public PipelineFactory PipelineFactory { get; }
    public SwapchainHandler SwapchainHandler { get; }
    
    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void FrameSetup()
    {
        throw new NotImplementedException();
    }

    public void FrameCleanup()
    {
        throw new NotImplementedException();
    }

    public void RecreateSwapchain()
    {
        throw new NotImplementedException();
    }
}