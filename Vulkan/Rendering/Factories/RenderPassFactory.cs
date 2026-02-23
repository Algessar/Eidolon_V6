using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class RenderPassFactory(VulkanMaster master)
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?
    VulkanMaster _master = master;
    private readonly Dictionary<RenderPassKey, RenderPass> _renderPassCache = new();
    
    
    public RenderPass CreateRenderPass(RenderPassKey key)
    {
        return CreateRenderPass(key, SampleCountFlags.Count1Bit, key.LoadOp, key.StoreOp);
    }
    public RenderPass CreateRenderPass(PassExecutionKey key)
    {
        return CreateRenderPass(ToRenderPassKey(key), key.SampleCount, key.DepthLoadOp, key.DepthStoreOp);
    }

    private RenderPass CreateRenderPass(RenderPassKey key, SampleCountFlags samples, 
        AttachmentLoadOp depthLoadOp, AttachmentStoreOp depthStoreOp)
    {
        if (_renderPassCache.TryGetValue(key, out var existing))
        {
            return existing;
        }
        
        var colorAttachment = new AttachmentDescription
        {
            Format = key.ColorFormat,
            Samples = samples,
            LoadOp = key.LoadOp,
            StoreOp = key.StoreOp,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = key.InitialLayout,
            FinalLayout = key.FinalLayout
        };

        // Create depth attachment only if needed
        var depthAttachment = new AttachmentDescription();
        bool hasDepth = key.HasDepth;
        
        if (hasDepth)
        {
            depthAttachment = new AttachmentDescription
            {
                Format = key.DepthFormat,
                Samples = samples,
                LoadOp = depthLoadOp,
                StoreOp = depthStoreOp,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = key.InitialDepthLayout,
                FinalLayout = key.FinalDepthLayout
            };
        }

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        var depthAttachmentRef = new AttachmentReference
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        // Only set depth attachment if we have one
        if (hasDepth)
        {
            subpass.PDepthStencilAttachment = &depthAttachmentRef;
        }

        // Dependency to ensure the render pass waits for the image to be available
        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        // Create attachments array based on whether we have depth
        RenderPass renderPass;
        if (hasDepth)
        {
            var attachments = stackalloc AttachmentDescription[2];
            attachments[0] = colorAttachment;
            attachments[1] = depthAttachment;

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 2,
                PAttachments = attachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            if (_master.Vk.CreateRenderPass(_master.VulkanDevice.Device, &renderPassInfo, null, out renderPass) != Result.Success)
                throw new Exception("Failed to create render pass!");
            
            Debug.Log($"[RenderPass] Color format={attachments[0].Format}, " +
                      $"loadOp={attachments[0].LoadOp}, " +
                      $"initialLayout={attachments[0].InitialLayout}, " +
                      $"finalLayout={attachments[0].FinalLayout}", VALIDATION_LAYERS.INFO);
        }
        else
        {
            var attachments = stackalloc AttachmentDescription[1];
            attachments[0] = colorAttachment;

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = attachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            if (_master.Vk.CreateRenderPass(_master.VulkanDevice.Device, &renderPassInfo, null, out renderPass) != Result.Success)
                throw new Exception("Failed to create render pass!");
            
            Debug.Log($"[RenderPass] Color format={attachments[0].Format}, " +
                      $"loadOp={attachments[0].LoadOp}, " +
                      $"initialLayout={attachments[0].InitialLayout}," +
                      $" finalLayout={attachments[0].FinalLayout}", VALIDATION_LAYERS.INFO);
        }
        
        _renderPassCache.Add(key, renderPass);
        return renderPass;
    }
    
    private static RenderPassKey ToRenderPassKey(PassExecutionKey key)
    {
        return new RenderPassKey
        {
            ColorFormat = key.ColorFormat,
            DepthFormat = key.DepthFormat,
            HasAlpha = true,
            HasDepth = key.DepthTargetHandle != 0,
            HasStencil = false,
            LoadOp = key.ColorLoadOp,
            StoreOp = key.ColorStoreOp,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
            InitialDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
            InitialLayout = ImageLayout.ColorAttachmentOptimal,
            FinalLayout = key.PassType == RenderPassType.Present ? ImageLayout.PresentSrcKhr : ImageLayout.ColorAttachmentOptimal
        };
    }
}