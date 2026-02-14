using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal struct GraphImageRuntime
{
    public Image Image;
    public ImageView View;
    public DeviceMemory Memory;
    public bool Imported;
    public Format Format;
    public Extent2D Extent;
    public FlagImageUsage Usage;
    public ImageLayout CurrentLayout;
}

internal sealed unsafe class GraphResourceRuntimeManager
{
    private readonly VulkanMaster _master;
    private readonly Dictionary<uint, GraphImageRuntime> _graphImages = new();
    private readonly Dictionary<uint, CompiledResource> _resourceLookup = new();

    public GraphResourceRuntimeManager(VulkanMaster master)
    {
        _master = master;
    }

    public void RegisterResource(in CompiledResource resource)
    {
        _resourceLookup[resource.Handle.Handle] = resource;
    }

    public void ClearResourceLookup() => _resourceLookup.Clear();

    public bool TryGetResource(uint handle, out CompiledResource resource) => _resourceLookup.TryGetValue(handle, out resource);

    public bool TryGetRuntime(uint handle, out GraphImageRuntime runtime) => _graphImages.TryGetValue(handle, out runtime);

    public void SetRuntime(uint handle, in GraphImageRuntime runtime) => _graphImages[handle] = runtime;

    public void EnsureGraphResources(CompiledRenderGraph graph, in SwapchainHandler swapchainHandler, bool logRenderGraph)
    {
        foreach (var resource in graph.Resources)
        {
            if (_graphImages.ContainsKey(resource.Handle.Handle))
                continue;

            if (logRenderGraph)
            {
                Debug.Log($"Creating runtime resource {resource.Name}"
                          + $"H:{resource.Handle.Handle}, Imported:{resource.Imported}"
                          + $"Use:{resource.FirstUsePass} -> {resource.LastUsePass}");
            }

            if (resource.Imported)
            {
                _graphImages[resource.Handle.Handle] = new GraphImageRuntime
                {
                    Imported = true,
                    Usage = resource.Description.Usage,
                    Format = ResolveVkFormat(resource.Description.Format),
                    Extent = ResolveGraphExtent(resource.Description, swapchainHandler),
                    CurrentLayout = ImageLayout.Undefined,
                };
                continue;
            }

            _graphImages[resource.Handle.Handle] = CreateGraphImage(resource, swapchainHandler);
        }
    }

    public void DestroyGraphResources()
    {
        foreach (var runtime in _graphImages.Values)
        {
            if (runtime.Imported)
                continue;

            if (runtime.View.Handle != 0)
                _master.Vk.DestroyImageView(_master.VulkanDevice.Device, runtime.View, null);

            if (runtime.Image.Handle != 0)
                _master.Vk.DestroyImage(_master.VulkanDevice.Device, runtime.Image, null);

            if (runtime.Memory.Handle != 0)
                _master.Vk.FreeMemory(_master.VulkanDevice.Device, runtime.Memory, null);
        }

        _graphImages.Clear();
    }

    public void ResolveImportedGraphResources(
        CompiledRenderGraph? compiledGraph,
        GraphResourceImportMap importMap,
        in SwapchainHandler swapchainHandler,
        uint currentImageIndex)
    {
        if (compiledGraph is null)
            return;

        foreach (var resource in compiledGraph.Resources)
        {
            if (!resource.Imported)
                continue;

            if (!importMap.TryResolve(resource, swapchainHandler, currentImageIndex, out var imageData))
                continue;

            var initialLayout = (resource.Description.Usage & FlagImageUsage.Present) != 0
                ? ImageLayout.PresentSrcKhr
                : ImageLayout.Undefined;

            _graphImages[resource.Handle.Handle] = new GraphImageRuntime
            {
                Image = imageData.Image,
                View = imageData.View,
                Memory = imageData.Memory,
                Imported = true,
                Format = imageData.Format,
                Extent = imageData.Extent,
                Usage = resource.Description.Usage,
                CurrentLayout = initialLayout,
            };
        }
    }

    private GraphImageRuntime CreateGraphImage(in CompiledResource resource, in SwapchainHandler swapchainHandler)
    {
        var format = ResolveVkFormat(resource.Description.Format);
        var usage = ResolveImageUsage(resource.Description.Usage);
        var extent = ResolveGraphExtent(resource.Description, swapchainHandler);

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(extent.Width, extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
        };

        if (_master.Vk.CreateImage(_master.VulkanDevice.Device, in imageInfo, null, out var image) != Result.Success)
            throw new Exception($"Failed to create graph image for resource '{resource.Name}'.");

        _master.Vk.GetImageMemoryRequirements(_master.VulkanDevice.Device, image, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, in allocInfo, null, out var memory) != Result.Success)
        {
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to allocate graph image memory for resource '{resource.Name}'.");
        }

        if (_master.Vk.BindImageMemory(_master.VulkanDevice.Device, image, memory, 0) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to bind graph image memory for resource '{resource.Name}'.");
        }

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ResolveAspectFlags(resource.Description.Usage, resource.Description.Format),
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        if (_master.Vk.CreateImageView(_master.VulkanDevice.Device, in viewInfo, null, out var view) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to create graph image view for resource '{resource.Name}'.");
        }

        Debug.Log($"[RG] Allocated runtime image '{resource.Name}' ({extent.Width}x{extent.Height}) format={format} usage={usage}");

        return new GraphImageRuntime
        {
            Image = image,
            View = view,
            Memory = memory,
            Imported = false,
            Format = format,
            Extent = extent,
            Usage = resource.Description.Usage,
            CurrentLayout = ImageLayout.Undefined,
        };
    }

    private static Format ResolveVkFormat(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Bgra8Unorm => Format.B8G8R8A8Unorm,
            ImageFormat.Rgba16Float => Format.R16G16B16A16Sfloat,
            ImageFormat.D24UnormS8Uint => Format.D24UnormS8Uint,
            ImageFormat.D32Float => Format.D32Sfloat,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported render-graph image format."),
        };
    }

    public static ImageAspectFlags ResolveAspectFlags(FlagImageUsage usage, ImageFormat format)
    {
        if ((usage & FlagImageUsage.DepthStencilAttachment) != 0)
        {
            return format == ImageFormat.D24UnormS8Uint
                ? ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit
                : ImageAspectFlags.DepthBit;
        }

        return ImageAspectFlags.ColorBit;
    }

    private static Extent2D ResolveGraphExtent(GraphImageDescription description, in SwapchainHandler swapchainHandler)
    {
        var width = Math.Max(1u, (uint)MathF.Round(swapchainHandler.Extent.Width * description.ScaleX));
        var height = Math.Max(1u, (uint)MathF.Round(swapchainHandler.Extent.Height * description.ScaleY));
        return new Extent2D(width, height);
    }

    private static ImageUsageFlags ResolveImageUsage(FlagImageUsage usage)
    {
        ImageUsageFlags result = 0;

        if ((usage & FlagImageUsage.ColorAttachment) != 0)
            result |= ImageUsageFlags.ColorAttachmentBit;
        if ((usage & FlagImageUsage.DepthStencilAttachment) != 0)
            result |= ImageUsageFlags.DepthStencilAttachmentBit;
        if ((usage & FlagImageUsage.Sampled) != 0)
            result |= ImageUsageFlags.SampledBit;
        if ((usage & FlagImageUsage.Storage) != 0)
            result |= ImageUsageFlags.StorageBit;
        if ((usage & FlagImageUsage.TransferSource) != 0)
            result |= ImageUsageFlags.TransferSrcBit;
        if ((usage & FlagImageUsage.TransferDestination) != 0)
            result |= ImageUsageFlags.TransferDstBit;

        if (result == 0)
            result = ImageUsageFlags.SampledBit;

        return result;
    }
}