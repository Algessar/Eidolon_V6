using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class SwapchainHandler
{
    VulkanMaster _master;
    
    private SurfaceKHR _surfaceKhr;
    private SwapchainKHR _swapchainKhr;
    private KhrSwapchain _khrSwapchain;
    private KhrSurface _khrSurface;
    
    public uint ImageCount;
    private ImageView[] _imageViews;
    private Image _depthImage;
    private ImageView _depthImageView;
    private DeviceMemory _depthImageMemory;
    private Format _depthFormat;
    public Extent2D Extent { get; private set; }
    public Framebuffer[] Framebuffers { get; set; }
    private Image[] SwapchainImages { get; set; }


    

    
    public SwapchainHandler(VulkanMaster master, SurfaceKHR surfaceKhr, KhrSurface khrSurface)
    {
        _master = master;
        _surfaceKhr = surfaceKhr;
        _khrSurface = khrSurface;
    }

    public Result AcquireNextImage(Semaphore waitSemaphore, Fence fence, out uint imageIndex)
    {
        imageIndex = 0;

        var result = _khrSwapchain.AcquireNextImage(
            _master.VulkanDevice.Device, 
            _swapchainKhr,
            ulong.MaxValue,
            waitSemaphore,
            fence, 
            ref imageIndex
        );

        return result;
    }

    public void QueueSubmit(CommandBuffer cmd, Semaphore wait, Semaphore signal, Fence fence)
    {
        PipelineStageFlags waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &wait,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signal
        };
        if (_master.Vk.QueueSubmit(_master.VulkanDevice.GraphicsQueue, 1, &submitInfo, fence) != Result.Success)
            throw new Exception("Failed to submit command buffer.");
    }

    public Result Present(Queue deviceManagerGraphicsQueue, Semaphore signalSemaphore, uint currentImageIndex)
    {
        var swap = _swapchainKhr;

        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &signalSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swap,
            PImageIndices = &currentImageIndex
        };

        return _khrSwapchain.QueuePresent(deviceManagerGraphicsQueue, &presentInfo);
    
    }

    public void CreateFramebuffers(RenderPass renderPass, bool renderPassHasDepth)
    {
        Debug.Log($"Creating framebuffers for {renderPass.Handle}...");
        // Cleanup existing framebuffers if necessary
        if (Framebuffers is { Length: > 0 })
        {
            foreach (var framebuffer in Framebuffers)
            {
                if (framebuffer.Handle != 0)
                {
                    _master.Vk.DestroyFramebuffer(_master.VulkanDevice.Device, framebuffer, null);
                }
            }
        }

        // Create swapchain framebuffers
        Framebuffers = new Framebuffer[_imageViews.Length];

        for (int i = 0; i < _imageViews.Length; i++)
        {
            Debug.Log($"Image view handles: {_imageViews}");
        }

        ImageView[] attachmentsArray;

        if (renderPassHasDepth)
        {
            // 2 attachments: color + depth
            attachmentsArray = new ImageView[2];
            attachmentsArray[1] = _depthImageView; // Depth is same for all
        }
        else
        {
            // 1 attachment: color only
            attachmentsArray = new ImageView[1];
        }

        fixed (ImageView* attachmentsPtr = attachmentsArray)
        {
            for (int i = 0; i < _imageViews.Length; i++)
            {
                // Update only the color attachment (changes per framebuffer)
                attachmentsArray[0] = _imageViews[i];

                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = renderPass,
                    AttachmentCount = (uint)attachmentsArray.Length,
                    PAttachments = attachmentsPtr,
                    Width = Extent.Width,
                    Height = Extent.Height,
                    Layers = 1
                };

                if (_master.Vk.CreateFramebuffer(_master.VulkanDevice.Device,
                        in framebufferInfo, null, out Framebuffers[i]) != Result.Success)
                {
                    throw new Exception($"Failed to create swapchain framebuffer {i}!");
                }
            }
        }
    }
}

