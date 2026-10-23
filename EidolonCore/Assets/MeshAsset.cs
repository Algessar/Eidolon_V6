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

    public long SizeInBytes { get; }

    /// <summary>The parsed mesh, or null until the asset has been imported.</summary>
    public Mesh? Mesh { get; internal set; }

    public bool IsLoaded => Mesh is not null;

    internal MeshAsset(string path, string name, long sizeInBytes)
    {
        Path = path;
        Name = name;
        SizeInBytes = sizeInBytes;
    }

    public override string ToString() => $"MeshAsset({Name})";
}
