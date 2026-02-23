using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class PassExecutionFactory(VulkanMaster master, Func<uint, PassAttachmentRuntime?> resolveAttachment)
{
    private readonly Dictionary<PassExecutionKey, RenderPass> _renderPassCache = new();
    private readonly Dictionary<FramebufferCacheKey, Framebuffer> _framebufferCache = new();

    private bool DEBUG = false;

    public PassExecutionContext GetOrCreate(in CompiledPass pass, in CompiledRenderGraph compiledGraph, uint currentImageIndex)
    {
        DEBUG = false;
        if(DEBUG)
        {
            Debug.Log($"Calling GetOrCreate for pass {pass.Name}");
        }        
        DEBUG = true;
        var resources = compiledGraph.Resources.ToDictionary(r => r.Handle.Handle, r => r);

        var colorTarget = ResolveColorTarget(pass, resources);

        if (DEBUG)
        {
            Debug.Log($"Pass '{pass.Name}': colorTarget {colorTarget.Name} :: ColorTarget Handle: {colorTarget.Handle.Handle}");
        }
        
        var depthTarget = ResolveDepthTarget(pass, resources);

        var colorRuntime = resolveAttachment(colorTarget.Handle.Handle)
            ?? throw new InvalidOperationException($"Missing runtime attachment for color target {colorTarget.Name}.");

        var depthRuntime = depthTarget is not null
            ? resolveAttachment(depthTarget.Handle.Handle)
            : null;

        if(DEBUG)
        {
            Debug.Log($"Pass '{pass.Name}': FirstUsePass={colorTarget.FirstUsePass}, ExecutionIndex={pass.ExecutionIndex}, IsFirstUse={colorTarget.FirstUsePass == pass.ExecutionIndex}");
        }
        
        var key = BuildKey(pass, colorTarget, colorRuntime, depthTarget, depthRuntime);
        var renderPass = GetOrCreateRenderPass(key);

        var framebuffer = GetOrCreateFramebuffer(
            renderPass,
            colorRuntime,
            depthRuntime,
            pass,
            currentImageIndex);

        
        return new PassExecutionContext(
            renderPass,
            framebuffer,
            colorRuntime.Extent,
            depthRuntime is not null,
            key.ColorLoadOp == AttachmentLoadOp.Clear,
            key.DepthLoadOp == AttachmentLoadOp.Clear && depthRuntime is not null);
    }

    public void Reset()
    {
        foreach (var framebuffer in _framebufferCache.Values)
        {
            if (framebuffer.Handle != 0)
            {
                master.Vk.DestroyFramebuffer(master.VulkanDevice.Device, framebuffer, null);
            }
        }
        
        _framebufferCache.Clear();
    }

    private Framebuffer GetOrCreateFramebuffer(
        RenderPass renderPass,
        in PassAttachmentRuntime colorRuntime,
        PassAttachmentRuntime? depthRuntime,
        in CompiledPass pass,
        uint currentImageIndex)
    {
        var framebufferKey = new FramebufferCacheKey(
            renderPass.Handle,
            colorRuntime.View.Handle,
            depthRuntime?.View.Handle ?? 0,
            colorRuntime.Extent.Width,
            colorRuntime.Extent.Height,
            pass.Type,
            currentImageIndex);

        if (_framebufferCache.TryGetValue(framebufferKey, out var existing))
        {
            return existing;
        }

        ImageView* attachments = stackalloc ImageView[depthRuntime is null ? 1 : 2];
        attachments[0] = colorRuntime.View;

        var attachmentCount = 1u;
        if (depthRuntime is not null)
        {
            attachments[1] = depthRuntime.Value.View;
            attachmentCount = 2;
        }

        var framebufferInfo = new FramebufferCreateInfo
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = renderPass,
            AttachmentCount = attachmentCount,
            PAttachments = attachments,
            Width = colorRuntime.Extent.Width,
            Height = colorRuntime.Extent.Height,
            Layers = 1
        };

        if (master.Vk.CreateFramebuffer(master.VulkanDevice.Device, in framebufferInfo, null, out var framebuffer) != Result.Success)
        {
            throw new Exception($"Failed to create framebuffer for pass '{pass.Name}'.");
        }

        if(DEBUG)
        {
            Debug.Log($"Created framebuffer {framebuffer.Handle} for pass '{pass.Name}'", VALIDATION_LAYERS.WARNING);
            //IMAGE HANDLE
            Debug.Log($"Framebuffer color attachment handle: {colorRuntime.View.Handle}", VALIDATION_LAYERS.WARNING);
        }
        

        _framebufferCache[framebufferKey] = framebuffer;
        return framebuffer;
    }

    private RenderPass GetOrCreateRenderPass(in PassExecutionKey key)
    {
        if (_renderPassCache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var renderPass = master.RenderPassFactory.CreateRenderPass(key);
        _renderPassCache[key] = renderPass;
        return renderPass;
    }

    private PassExecutionKey BuildKey(
        in CompiledPass pass,
        in CompiledResource colorTarget,
        in PassAttachmentRuntime colorRuntime,
        CompiledResource? depthTarget,
        PassAttachmentRuntime? depthRuntime)
    {
        var isFirstColorUse = colorTarget.FirstUsePass == pass.ExecutionIndex;
        var colorLoadOp = isFirstColorUse ? AttachmentLoadOp.Clear : AttachmentLoadOp.Load;

        var hasDepth = depthTarget is not null;
        var isFirstDepthUse = depthTarget is { } depthResource && depthResource.FirstUsePass == pass.ExecutionIndex;

        return new PassExecutionKey(
            pass.Type,
            colorTarget.Handle.Handle,
            depthTarget?.Handle.Handle ?? 0,
            colorRuntime.Format,
            hasDepth && depthRuntime is not null ? depthRuntime.Value.Format : Format.Undefined,
            colorLoadOp,
            AttachmentStoreOp.Store,
            hasDepth ? (isFirstDepthUse ? AttachmentLoadOp.Clear : AttachmentLoadOp.Load) : AttachmentLoadOp.DontCare,
            hasDepth ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare,
            pass.Type is RenderPassType.Geometry,
            pass.Type is RenderPassType.PostProcess or RenderPassType.Ui,
            SampleCountFlags.Count1Bit);
    }

    private CompiledResource ResolveColorTarget(in CompiledPass pass, IReadOnlyDictionary<uint, CompiledResource> resources)
    {
        foreach (var write in pass.Writes)
        {
            if (!resources.TryGetValue(write.Handle, out var resource))
                continue;

            if ((resource.Description.Usage & (FlagImageUsage.ColorAttachment | FlagImageUsage.Present)) != 0)
                return resource;
        }

        foreach (var read in pass.Reads)
        {
            if (!resources.TryGetValue(read.Handle, out var resource))
                continue;

            if ((resource.Description.Usage & FlagImageUsage.Present) != 0)
                return resource;
        }

        throw new InvalidOperationException($"Pass '{pass.Name}' has no graph-driven color target.");
    }
    
    // NOTE: Why is this static?
    private CompiledResource? ResolveDepthTarget(in CompiledPass pass, IReadOnlyDictionary<uint, CompiledResource> resources)
    {
        foreach (var write in pass.Writes)
        {
            if (!resources.TryGetValue(write.Handle, out var resource))
                continue;

            if ((resource.Description.Usage & FlagImageUsage.DepthStencilAttachment) != 0)
                return resource;
        }

        return null;
    }

    // NOTE: Why is this static? And why is it unused? 
    private Format ResolveVkFormat(ImageFormat format)
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

    private readonly record struct FramebufferCacheKey(
        ulong RenderPassHandle,
        ulong ColorViewHandle,
        ulong DepthViewHandle,
        uint Width,
        uint Height,
        RenderPassType PassType,
        uint ImageIndex);
}

internal readonly record struct PassExecutionContext(
    RenderPass RenderPass,
    Framebuffer Framebuffer,
    Extent2D Extent,
    bool HasDepth,
    bool ClearColor,
    bool ClearDepth);

internal readonly record struct PassAttachmentRuntime(
    ImageView View,
    Format Format,
    Extent2D Extent,
    FlagImageUsage Usage);