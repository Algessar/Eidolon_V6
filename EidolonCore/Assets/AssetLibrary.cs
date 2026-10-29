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

    /// <summary>Model formats the host can import. The engine only lists them so the browser shows them.</summary>
    private static readonly string[] ModelExtensions = [".fbx", ".gltf", ".glb", ".dae", ".3ds", ".blend"];

    private readonly Dictionary<string, Mesh> _loadedMeshes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MeshAsset> _assetsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshAsset> _catalog = [];

    /// <summary>The directory the last <see cref="Discover"/> call scanned, if any.</summary>
    public string? RootDirectory { get; private set; }

    /// <summary>
    /// Folder tree under <see cref="RootDirectory"/>, as browsed in the editor. Null until a
    /// successful <see cref="Discover"/>.
    /// </summary>
    public AssetFolder? Root { get; private set; }

    /// <summary>Every discovered asset, flat and sorted by name.</summary>
    public IReadOnlyList<MeshAsset> Catalog => _catalog;

    public IReadOnlyCollection<Mesh> LoadedMeshes => _loadedMeshes.Values;

    public int MeshCount => _loadedMeshes.Count;

    /// <summary>
    /// Scans a directory tree for importable mesh files and rebuilds <see cref="Catalog"/> and
    /// <see cref="Root"/>. Returns the number of assets found.
    /// </summary>
    public int Discover(string directory)
    {
        RootDirectory = Path.GetFullPath(directory);
        _catalog.Clear();
        _assetsByPath.Clear();
        Root = null;

        if (!Directory.Exists(RootDirectory))
            return 0;

        var foldersByPath = new Dictionary<string, AssetFolder>(StringComparer.OrdinalIgnoreCase);
        Root = BuildFolderTree(RootDirectory, foldersByPath);

        foreach (var path in Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories))
        {
            var isMesh = HasMeshExtension(path);
            var isModel = HasModelExtension(path);

            if (!isMesh && !isModel)
                continue;

            var fullPath = Path.GetFullPath(path);
            var folderPath = Path.GetDirectoryName(fullPath);

            if (folderPath is null || !foldersByPath.TryGetValue(folderPath, out var folder))
                continue;

            var relativePath = Path.GetRelativePath(RootDirectory, fullPath).Replace('\\', '/');
            var asset = new MeshAsset(
                fullPath,
                Path.GetFileNameWithoutExtension(fullPath),
                relativePath,
                new FileInfo(fullPath).Length,
                isModel)
            {
                // Keep assets that were imported before a rescan marked as loaded.
                Mesh = isMesh ? TryGetMesh(fullPath) : null,
            };

            folder.AddFile(asset);
            _catalog.Add(asset);
            _assetsByPath[fullPath] = asset;
        }

        _catalog.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        Root.SortContentsRecursive();
        Root.CountFiles();

        return _catalog.Count;
    }

    /// <summary>Re-scans the directory from the last <see cref="Discover"/> call.</summary>
    public int Refresh() => RootDirectory is null ? 0 : Discover(RootDirectory);

    public Mesh? TryGetMesh(string path) =>
        _loadedMeshes.TryGetValue(Path.GetFullPath(path), out var mesh) ? mesh : null;

    /// <summary>Looks up a discovered asset by absolute path.</summary>
    public bool TryGetAsset(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MeshAsset? asset)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            asset = null;
            return false;
        }

        try
        {
            return _assetsByPath.TryGetValue(Path.GetFullPath(path), out asset);
        }
        catch (ArgumentException)
        {
            // Treat malformed paths as a miss; callers of TryGetAsset should not have to
            // pre-validate strings that may have come from a native UI drag payload.
            asset = null;
            return false;
        }
    }

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
        if (_assetsByPath.TryGetValue(key, out var asset))
            asset.Mesh = mesh;

        return mesh;
    }

    /// <summary>Imports a catalog entry, loading it on first use.</summary>
    public Mesh Load(MeshAsset asset)
    {
        if (asset.IsModel)
            throw new InvalidOperationException($"'{asset.Name}' is a model file; the host imports it instead.");

        return asset.Mesh ??= LoadObj(asset.Path);
    }

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

    private static AssetFolder BuildFolderTree(string path, Dictionary<string, AssetFolder> foldersByPath)
    {
        var folder = new AssetFolder(path, FolderName(path));
        foldersByPath[path] = folder;

        foreach (var child in Directory.EnumerateDirectories(path))
        {
            // Skip the usual repository metadata so it does not clutter the browser.
            if (IsIgnoredFolder(child))
                continue;

            folder.AddSubfolder(BuildFolderTree(Path.GetFullPath(child), foldersByPath));
        }

        return folder;
    }

    private static string FolderName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);

        // A drive root ("G:\") has no file name of its own.
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private static bool IsIgnoredFolder(string path)
    {
        var name = Path.GetFileName(path);
        return name is ".git" or ".idea" or ".vs" or "bin" or "obj";
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

    private static bool HasModelExtension(string path)
    {
        var extension = Path.GetExtension(path);

        foreach (var candidate in ModelExtensions)
        {
            if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
