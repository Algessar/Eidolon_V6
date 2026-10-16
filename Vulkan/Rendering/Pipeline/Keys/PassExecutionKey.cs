using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal readonly record struct PassExecutionKey(
    RenderPassType PassType,
    uint ColorTargetHandle,
    uint DepthTargetHandle,
    Format ColorFormat,
    Format DepthFormat,
    AttachmentLoadOp ColorLoadOp,
    AttachmentStoreOp ColorStoreOp,
    AttachmentLoadOp DepthLoadOp,
    AttachmentStoreOp DepthStoreOp,
    bool DepthTestEnabled,
    bool BlendEnabled,
    SampleCountFlags SampleCount
);