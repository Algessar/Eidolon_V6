namespace EidolonCore.Assets;

/// <summary>
/// One folder of the project's asset tree, as shown in the editor's Browser. The tree is built
/// once per scan and read-only afterwards.
/// </summary>
public sealed class AssetFolder
{
    private readonly List<AssetFolder> _subfolders = [];
    private readonly List<MeshAsset> _files = [];

    internal AssetFolder(string path, string name)
    {
        Path = path;
        Name = name;
    }

    /// <summary>Display name: the last path segment.</summary>
    public string Name { get; }

    /// <summary>Full path on disk.</summary>
    public string Path { get; }

    public AssetFolder? Parent { get; private set; }

    public IReadOnlyList<AssetFolder> Subfolders => _subfolders;

    /// <summary>Assets directly in this folder, sorted by name.</summary>
    public IReadOnlyList<MeshAsset> Files => _files;

    public bool IsEmpty => _subfolders.Count == 0 && _files.Count == 0;

    /// <summary>Number of assets in this folder and everything below it.</summary>
    public int TotalFileCount { get; private set; }

    internal void AddSubfolder(AssetFolder folder)
    {
        folder.Parent = this;
        _subfolders.Add(folder);
    }

    internal void AddFile(MeshAsset asset) => _files.Add(asset);

    internal void SortContents()
    {
        _subfolders.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        _files.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sorts this folder and every folder below it.</summary>
    internal void SortContentsRecursive()
    {
        SortContents();

        foreach (var subfolder in _subfolders)
            subfolder.SortContentsRecursive();
    }

    /// <summary>Recalculates <see cref="TotalFileCount"/> for this folder and its children.</summary>
    internal int CountFiles()
    {
        var total = _files.Count;

        foreach (var subfolder in _subfolders)
            total += subfolder.CountFiles();

        TotalFileCount = total;
        return total;
    }

    /// <summary>
    /// Walks up the tree building a "Root/Models/Props" style path for display.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();

        for (var folder = this; folder is not null; folder = folder.Parent)
            parts.Add(folder.Name);

        parts.Reverse();
        return string.Join('/', parts);
    }
}
