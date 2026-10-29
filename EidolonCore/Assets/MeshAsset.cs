using EidolonCore.Rendering;

namespace EidolonCore.Assets;

/// <summary>
/// A mesh file discovered on disk. The mesh itself is loaded lazily, so a large asset folder can
/// be listed without parsing every file up front.
/// </summary>
public sealed class MeshAsset
{
    public string Path { get; }

    public string Name { get; }

    /// <summary>Path relative to the asset root, using '/' separators; handy for search results.</summary>
    public string RelativePath { get; }

    public long SizeInBytes { get; }

    /// <summary>The parsed mesh, or null until the asset has been imported.</summary>
    public Mesh? Mesh { get; internal set; }

    /// <summary>
    /// True for model files (FBX, glTF, DAE...) the host imports instead of parsing here: they can contain
    /// several meshes, a skeleton and animation, so the engine only ever lists them.
    /// </summary>
    public bool IsModel { get; }

    public bool IsLoaded => Mesh is not null;

    internal MeshAsset(string path, string name, string relativePath, long sizeInBytes, bool isModel = false)
    {
        Path = path;
        Name = name;
        RelativePath = relativePath;
        SizeInBytes = sizeInBytes;
        IsModel = isModel;
    }

    public override string ToString() => $"MeshAsset({Name})";
}
