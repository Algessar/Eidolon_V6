using System.Numerics;
using Eidolon.Engine;
using Eidolon.Vulkan;

namespace EidolonEngine;

public sealed class Scene
{
    private readonly List<MeshInstance> _instances = new();

    public IReadOnlyList<MeshInstance> Instances => _instances;

    public void Add(MeshInstance instance) => _instances.Add(instance);
    public void Clear() => _instances.Clear();
}

public sealed class MeshInstance
{
    public Mesh Mesh;
    public Matrix4x4 Transform;
}