namespace Eidolon.Vulkan;

[Flags]
public enum FlagImageUsage : uint
{
    None = 0,
    ColorAttachment = 1 << 0,
    DepthStencilAttachment = 1 << 1,
    Sampled = 1 << 2,
    Storage = 1 << 3,
    TransferSource = 1 << 4,
    TransferDestination = 1 << 5,
    Present = 1 << 6
}