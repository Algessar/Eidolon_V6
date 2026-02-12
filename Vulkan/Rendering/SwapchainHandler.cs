using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan;

internal unsafe class SwapchainHandler
{
    VulkanMaster _master;
    IWindow _window;
    
    private SurfaceKHR _surfaceKhr;
    private SwapchainKHR _swapchainKhr;
    private KhrSwapchain _khrSwapchain;
    private KhrSurface _khrSurface;
    private Format _swapchainImageFormat;
    public Format SwapchainImageFormat => _swapchainImageFormat;
    
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
        Debug.Log("Creating SwapchainHandler", VALIDATION_LAYERS.INFO);
        _master = master;
        _window = master.GetWindow;
        _surfaceKhr = surfaceKhr;
        _khrSurface = khrSurface;
        
        CreateSwapchain();
        CreateImageViews();
        CreateDepthResources();
        
        Debug.Log("SwapchainHandler created!", VALIDATION_LAYERS.SUCCESS);
    }

    public ImageData GetSwapchainColorImageData(uint imageIndex)
    {
        if (SwapchainImages is null || _imageViews is null)
        {
            throw new Exception("Swapchain images not initialized!");
        }

        if (imageIndex >= SwapchainImages.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(imageIndex));
        }

        return new ImageData
        {
            Image = SwapchainImages[imageIndex],
            View = _imageViews[imageIndex],
            Format = default,
            Extent = Extent,
            Memory = default
        };
    }
    
    public ImageData GetDepthImageData()
    {
        
        if (_depthImage.Handle == 0 || _depthImageView.Handle == 0)
        {
            throw new InvalidOperationException("Depth image is not initialized.");
        }

        return new ImageData
        {
            Image = _depthImage,
            View = _depthImageView,
            Format = _depthFormat,
        };
    }


    private void CreateImageViews()
    {
        _imageViews = new ImageView[SwapchainImages.Length];

        for (int i = 0; i < SwapchainImages.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = SwapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainImageFormat,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_master.Vk.CreateImageView(_master.VulkanDevice.Device, in createInfo, null,
                    out _imageViews[i]) != Result.Success)
            {
                throw new Exception($"Failed to create image view {i}!");
            }
        }
    }


     private void CreateDepthResources()
    {
        _depthFormat = Format.D32Sfloat;

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(Extent.Width, Extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = _depthFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_master.Vk.CreateImage(_master.VulkanDevice.Device, in imageInfo, null, out _depthImage) != Result.Success)
            throw new Exception("Failed to create depth image!");

        _master.Vk.GetImageMemoryRequirements(_master.VulkanDevice.Device, _depthImage, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex =
                _master.VulkanDevice.FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, in allocInfo, null, out _depthImageMemory) !=
            Result.Success)
            throw new Exception("Failed to allocate depth image memory!");

        _master.Vk.BindImageMemory(_master.VulkanDevice.Device, _depthImage, _depthImageMemory, 0);

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _depthImage,
            ViewType = ImageViewType.Type2D,
            Format = _depthFormat,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.DepthBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (_master.Vk.CreateImageView(_master.VulkanDevice.Device, in viewInfo, null, out _depthImageView) !=
            Result.Success)
            throw new Exception("Failed to create depth image view!");
    }

    private void CreateSwapchain()
    {
        if (!_master.Vk.TryGetDeviceExtension(_master.VulkanInstance.Instance, _master.VulkanDevice.Device,
                out _khrSwapchain))
        {
            Debug.Log("Failed to load KHR swapchain extension!", VALIDATION_LAYERS.ERROR);
        }

        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_master.VulkanDevice.PhysicalDevice, _surfaceKhr,
            out var surfaceCapabilities);

        if (surfaceCapabilities.CurrentExtent.Width == 0 || surfaceCapabilities.CurrentExtent.Height == 0)
        {
            _swapchainKhr = default;
            Debug.Log("Surface size is undefined!", VALIDATION_LAYERS.ERROR);
        }

        uint formatCount = 0;
        
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_master.VulkanDevice.PhysicalDevice, _surfaceKhr, ref formatCount,
            null);
        var surfaceFormats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* pSurfaceFormats = surfaceFormats)
        {
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_master.VulkanDevice.PhysicalDevice, _surfaceKhr,
                ref formatCount, pSurfaceFormats);
        }


        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(_master.VulkanDevice.PhysicalDevice, _surfaceKhr,
            ref presentModeCount, null);
        var presentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* pPresentModes = presentModes)
        {
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(_master.VulkanDevice.PhysicalDevice, _surfaceKhr,
                ref presentModeCount, pPresentModes);
        }
        
        var surfaceFormat = ChooseSwapSurfaceFormat(surfaceFormats);
        var presentMode = ChooseSwapPresentMode(presentModes);
        var extent = ChooseSwapExtent(surfaceCapabilities);

        uint imageCount = surfaceCapabilities.MinImageCount + 1;
        // var presentImages = new Image[presentModeCount];
        if (surfaceCapabilities.MinImageCount > 0 && imageCount > surfaceCapabilities.MaxImageCount)
        {
            imageCount = surfaceCapabilities.MaxImageCount;
        }

        var preTransform = surfaceCapabilities.CurrentTransform;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surfaceKhr,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = preTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        if (_surfaceKhr.Handle == 0)
        {
            Debug.Log("SurfaceKHR in SwapchainManager is null");
        }

        var indices = _master.VulkanDevice.FindQueueFamilies(_master.VulkanDevice.PhysicalDevice);
        uint[] queueFamilyIndices = { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

        if (indices.GraphicsFamily != indices.PresentFamily)
        {
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;

            fixed (uint* queueFamilyIndicesPtr = queueFamilyIndices)
            {
                createInfo.PQueueFamilyIndices = queueFamilyIndicesPtr;

                if (_khrSwapchain.CreateSwapchain(_master.VulkanDevice.Device, &createInfo, null, out _swapchainKhr) !=
                    Result.Success)
                {
                    throw new Exception("Failed to create swapchain!");
                }
            }
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
            createInfo.QueueFamilyIndexCount = 0;
            createInfo.PQueueFamilyIndices = null;

            if (_khrSwapchain.CreateSwapchain(_master.VulkanDevice.Device, &createInfo, null, out _swapchainKhr) !=
                Result.Success)
            {
                throw new Exception("Failed to create swapchain!");
            }
        }

        _swapchainImageFormat = surfaceFormat.Format;
        Extent = extent;

        ImageCount = 0;
        _khrSwapchain.GetSwapchainImages(_master.VulkanDevice.Device, _swapchainKhr, ref ImageCount, null);

        SwapchainImages = new Image[ImageCount];
        fixed (Image* swapchainImagesPtr = SwapchainImages)
        {
            _khrSwapchain.GetSwapchainImages(_master.VulkanDevice.Device, _swapchainKhr, ref ImageCount,
                swapchainImagesPtr);
        }
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] surfaceFormats)
    {
        foreach (var format in surfaceFormats)
        {
            if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return format;
            }
        }

        // Otherwise just use the first available
        return surfaceFormats[0];
    }

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] presentModes)
    {
        // Prefer mailbox (triple buffering) if available
        foreach (var mode in presentModes)
        {
            if (mode == PresentModeKHR.FifoKhr)
            {
                return mode;
            }
        }

        // Otherwise use FIFO (vsync) which is always available
        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR pSurfaceCapabilities)
    {
        
        if (pSurfaceCapabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return pSurfaceCapabilities.CurrentExtent;
        }

        // Otherwise choose based on window size
        var actualExtent = new Extent2D
        {
            Width = (uint)_window.FramebufferSize.X,
            Height = (uint)_window.FramebufferSize.Y
        };

        // Clamp to min/max bounds
        actualExtent.Width = Math.Clamp(actualExtent.Width, pSurfaceCapabilities.MinImageExtent.Width,
            pSurfaceCapabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, pSurfaceCapabilities.MinImageExtent.Height,
            pSurfaceCapabilities.MaxImageExtent.Height);

        return actualExtent;

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

    public Result Present( Semaphore signalSemaphore, uint currentImageIndex)
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

        return _khrSwapchain.QueuePresent(_master.VulkanDevice.PresentQueue, &presentInfo);
    
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
            Debug.Log($"Image view handle[{i}]: {_imageViews[i].Handle}");
            
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

    public bool RecreateSwapchain(RenderPass renderPass, bool renderPassHasDepth)
    {
        while (_window.FramebufferSize.X == 0 || _window.FramebufferSize.Y == 0)
        {
            return false;
        }

        _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);

        CleanupSwapchainResources();

        CreateSwapchain();
        CreateImageViews();
        CreateDepthResources();
        CreateFramebuffers(renderPass, renderPassHasDepth);
        return true;
    }

    private void CleanupSwapchainResources()
    {
        if (Framebuffers is { Length: > 0 })
        {
            foreach (var framebuffer in Framebuffers)
            {
                if (framebuffer.Handle != 0)
                {
                    _master.Vk.DestroyFramebuffer(_master.VulkanDevice.Device, framebuffer, null);
                }
            }
            Framebuffers = Array.Empty<Framebuffer>();
        }

        if (_imageViews is { Length: > 0 })
        {
            foreach (var imageView in _imageViews)
            {
                if (imageView.Handle != 0)
                {
                    _master.Vk.DestroyImageView(_master.VulkanDevice.Device, imageView, null);
                }
            }
            _imageViews = Array.Empty<ImageView>();
        }

        if (_depthImageView.Handle != 0)
        {
            _master.Vk.DestroyImageView(_master.VulkanDevice.Device, _depthImageView, null);
            _depthImageView = default;
        }

        if (_depthImage.Handle != 0)
        {
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, _depthImage, null);
            _depthImage = default;
        }

        if (_depthImageMemory.Handle != 0)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, _depthImageMemory, null);
            _depthImageMemory = default;
        }

        if (_swapchainKhr.Handle != 0)
        {
            _khrSwapchain.DestroySwapchain(_master.VulkanDevice.Device, _swapchainKhr, null);
            _swapchainKhr = default;
        }
    }
}

