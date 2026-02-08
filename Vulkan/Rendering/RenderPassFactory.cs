using Silk.NET.Vulkan;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class RenderPassFactory(VulkanMaster master)
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?
    VulkanMaster _master = master;
    private readonly Dictionary<RenderPassKey, RenderPass> _renderPassCache = new();
    

public RenderPass CreateRenderPass(RenderPassKey key)
    {
        if (_renderPassCache.TryGetValue(key, out var existing))
        {
            return existing;
        }
        
        var colorAttachment = new AttachmentDescription
        {
            Format = key.ColorFormat,
            Samples = SampleCountFlags.Count1Bit,
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
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = key.LoadOp,
                StoreOp = key.StoreOp,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = key.InitialLayout,
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
        }
        
        _renderPassCache.Add(key, renderPass);
        return renderPass;
    }
}