using EidolonCore.Rendering;

namespace EidolonCore.Assets;

/// <summary>
/// Loads assets from disk, caches them, and lists what is available to import.
/// </summary>
/// <remarks>
/// Loading is cached by full path, so importing the same file twice returns the same
/// <see cref="Mesh"/> instance and therefore the same GPU buffers.
/// </remarks>
public sealed class AssetLibrary
{
    private static readonly string[] MeshExtensions = [".obj"];

    private readonly Dictionary<string, Mesh> _loadedMeshes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshAsset> _catalog = [];

    /// <summary>The directory the last <see cref="Discover"/> call scanned, if any.</summary>
    public string? RootDirectory { get; private set; }

    public IReadOnlyList<MeshAsset> Catalog => _catalog;

    public IReadOnlyCollection<Mesh> LoadedMeshes => _loadedMeshes.Values;

    public int MeshCount => _loadedMeshes.Count;

    /// <summary>
    /// Scans a directory tree for importable mesh files and rebuilds <see cref="Catalog"/>.
    /// Returns the number of assets found.
    /// </summary>
    public int Discover(string directory)
    {
        RootDirectory = Path.GetFullPath(directory);
        _catalog.Clear();

        if (!Directory.Exists(RootDirectory))
            return 0;

        foreach (var path in Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories))
        {
            if (!HasMeshExtension(path))
                continue;

            var fullPath = Path.GetFullPath(path);
            var info = new FileInfo(fullPath);

            _catalog.Add(new MeshAsset(fullPath, Path.GetFileNameWithoutExtension(fullPath), info.Length));
        }

        _catalog.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        return _catalog.Count;
    }

    /// <summary>Re-scans the directory from the last <see cref="Discover"/> call.</summary>
    public int Refresh() => RootDirectory is null ? 0 : Discover(RootDirectory);

    public Mesh? TryGetMesh(string path) =>
        _loadedMeshes.TryGetValue(Path.GetFullPath(path), out var mesh) ? mesh : null;

    /// <summary>Imports an OBJ file, returning the cached mesh if it was already loaded.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="FormatException">The file is not valid OBJ data.</exception>
    public Mesh LoadObj(string path)
    {
        var key = Path.GetFullPath(path);

        if (_loadedMeshes.TryGetValue(key, out var cached))
            return cached;

        var mesh = ObjLoader.Load(key);
        _loadedMeshes[key] = mesh;

        // Keep the catalog entry in sync if this file is one we discovered.
        foreach (var asset in _catalog)
        {
            if (string.Equals(asset.Path, key, StringComparison.OrdinalIgnoreCase))
            {
                asset.Mesh = mesh;
                break;
            }
        }

        return mesh;
    }

    /// <summary>Imports a catalog entry, loading it on first use.</summary>
    public Mesh Load(MeshAsset asset) => asset.Mesh ??= LoadObj(asset.Path);

    /// <summary>
    /// Same as <see cref="Load(MeshAsset)"/> but reports failure as a message instead of throwing,
    /// which is what UI callers want.
    /// </summary>
    public bool TryLoad(MeshAsset asset, out Mesh? mesh, out string? error)
    {
        try
        {
            mesh = Load(asset);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            mesh = null;
            error = ex.Message;
            return false;
        }
    }

    private static bool HasMeshExtension(string path)
    {
        foreach (var extension in MeshExtensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
