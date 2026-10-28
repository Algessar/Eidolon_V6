using EidolonCore.ECS;

namespace EidolonCore.Rendering;

/// <summary>
/// Marks an entity as drawable. The mesh is CPU-side data; the renderer uploads it to the GPU
/// the first time it sees it and caches the result, so this component never holds a device handle.
/// </summary>
/// <remarks>
/// Scene data rather than renderer state: a game says what it wants drawn without knowing how, which is
/// why the component lives in the engine while the code that draws it lives in the renderer.
/// </remarks>
public sealed class MeshRenderer : IComponent
{
    public Mesh? Mesh;

    public Material Material = new();

    public MeshRenderer()
    {
    }

    /// <summary>Copies the renderer for a snapshot: shares the mesh, copies the material.</summary>
    public MeshRenderer(MeshRenderer other)
    {
        Mesh = other.Mesh;
        Material = new Material(other.Material.Name, other.Material.BaseColor);
    }
    public MeshRenderer(Mesh mesh, Material? material = null)
    {
        Mesh = mesh;
        Material = material ?? new Material();
    }
}
