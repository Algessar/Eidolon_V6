using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

/// <summary>
/// Marks an entity as drawable. The mesh is CPU-side data; the renderer uploads it to the GPU
/// the first time it sees it and caches the result, so this component never holds a device handle.
/// </summary>
public sealed class MeshRenderer : IComponent
{
    public Mesh? Mesh;

    public Material Material = new();

    public MeshRenderer()
    {
    }

    public MeshRenderer(Mesh mesh, Material? material = null)
    {
        Mesh = mesh;
        Material = material ?? new Material();
    }
}
