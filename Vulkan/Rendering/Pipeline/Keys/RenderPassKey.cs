using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

/*
 * More variants:
 * ShadowPass
 * PostProcess
 * MSAA
 * Depth Prepass
 * 
 */

internal record struct RenderPassKey
{
    public Format ColorFormat { get; set; }
    public Format DepthFormat { get; set; }
    public bool HasAlpha { get; set; }
    public bool HasDepth { get; set; }
    public bool HasStencil { get; set; }
    public AttachmentLoadOp LoadOp { get; set; }
    public AttachmentStoreOp StoreOp { get; set; }

    public AttachmentLoadOp StencilLoadOp;
    public AttachmentStoreOp StencilStoreOp;
    
    public ImageLayout FinalDepthLayout { get; set; } 
    public ImageLayout InitialDepthLayout { get; set; }
    public ImageLayout InitialLayout { get; set; }
    public ImageLayout FinalLayout { get; set; }
}