namespace Eidolon.Vulkan;
using EidolonCore.Rendering;

internal enum ImportedResourceKind
{
    SwapchainColor,
    SceneDepth,
}

internal sealed class GraphResourceImportMap
{
    private readonly Dictionary<uint, ImportedResourceKind> _importKinds = new();

    public void Register(ResourceHandle handle, ImportedResourceKind kind)
    {
        if(handle.Handle == 0)
        {
            throw new ArgumentException("Resource handle must be non-zero.", nameof(handle));
        }
        
        _importKinds[handle.Handle] = kind;
    }

public bool TryResolve(
        in CompiledResource resource,
        in SwapchainHandler swapchain,
        uint swapchainImageIndex,
        out ImageData imageData)
    {
        imageData = default;

        if (!resource.Imported)
        {
            return false;
        }

        if (!_importKinds.TryGetValue(resource.Handle.Handle, out var kind))
        {
            return false;
        }

        imageData = kind switch
        {
            ImportedResourceKind.SwapchainColor => swapchain.GetSwapchainColorImageData(swapchainImageIndex),
            ImportedResourceKind.SceneDepth => swapchain.GetDepthImageData(),
            _ => throw new ArgumentOutOfRangeException()
        };

        return true;
    }
    
}