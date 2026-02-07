using EidolonCore.Resources;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Eidolon.Vulkan;

internal class Surfaces : IResource
{
    public KhrSurface KhrSurface;
    public SurfaceKHR SurfaceKhr;
    
}