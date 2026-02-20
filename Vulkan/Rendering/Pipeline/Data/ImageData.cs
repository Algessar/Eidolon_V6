using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal struct ImageData
{
    public Image Image;
    public DeviceMemory Memory;
    public ImageView View;
    public Format Format;
    public Extent2D Extent;
}

