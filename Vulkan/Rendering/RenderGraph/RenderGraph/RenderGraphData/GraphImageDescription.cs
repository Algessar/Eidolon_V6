namespace Eidolon.Vulkan;

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