namespace EidolonCore.Rendering;

public struct GraphImageDescription
{
    public ImageFormat Format;
    public FlagImageUsage Usage;

    public float ScaleX;   // 1.0 = full size
    public float ScaleY;

    public static GraphImageDescription Create(
        ImageFormat format,
        FlagImageUsage usage,
        float scaleX = 1.0f,
        float scaleY = 1.0f)
    {
        return new GraphImageDescription
        {
            Format = format,
            Usage = usage,
            ScaleX = scaleX,
            ScaleY = scaleY
        };
    }
}

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

public enum ImageFormat
{
    Unknown = 0,
    Rgba8Unorm,
    Bgra8Unorm,
    Rgba16Float,
    D24UnormS8Uint,
    D32Float,
}

