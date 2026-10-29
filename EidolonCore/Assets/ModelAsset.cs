using System.Numerics;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;

namespace EidolonCore.Assets;

/// <summary>One node of an imported model: a local transform, an optional mesh, and children.</summary>
public sealed class ModelNode
{
    public string Name = "Node";

    public Vector3 Position;

    public Quaternion Rotation = Quaternion.Identity;

    public Vector3 Scale = Vector3.One;

    /// <summary>Geometry attached to this node, or null for a pure bone/transform node.</summary>
    public Mesh? Mesh;

    /// <summary>Indices into <see cref="ModelAsset.Nodes"/>.</summary>
    public List<int> Children = [];
}

/// <summary>
/// Format-neutral imported model: a node tree with meshes, plus any animation clips the file carried.
/// </summary>
/// <remarks>
/// This is what an importer produces and what <see cref="ModelSpawner"/> consumes, so nothing downstream
/// knows whether the file was FBX, DAE, glTF or OBJ. Node names become entity names, which is what lets
/// an imported clip bind by path exactly like an authored one.
/// </remarks>
public sealed class ModelAsset
{
    public string Name = "Model";

    /// <summary>The node tree, root first.</summary>
    public List<ModelNode> Nodes = [];

    /// <summary>Clips keyed by node path, relative to the root.</summary>
    public List<AnimationClip> Clips = [];
}
