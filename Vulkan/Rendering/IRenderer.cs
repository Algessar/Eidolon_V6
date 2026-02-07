using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal interface IRenderer : IDisposable
{
    Framebuffer Framebuffer { get; }
    GpuBuffer GpuBuffer { get; }
    GpuImage GpuImage { get; }
    
    SwapchainHandler SwapchainHandler { get; }
    PipelineFactory PipelineFactory { get; }
    
    
    void Initialize();

    void FrameSetup();
    
    void FrameCleanup();

    void RecreateSwapchain(); //Call Swapchain Recreate here
}