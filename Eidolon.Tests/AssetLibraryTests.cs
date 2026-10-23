using EidolonCore.Assets;

namespace Eidolon.Tests;

public sealed class AssetLibraryTests : IDisposable
{
    private const string TriangleObj = """
        v 0 0 0
        v 1 0 0
        v 0 1 0
        f 1 2 3
        """;

    private readonly string _root;

    public AssetLibraryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "eidolon-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string CreateFile(string relativePath, string contents = TriangleObj)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string[] Names(IEnumerable<AssetFolder> folders) =>
        folders.Select(folder => folder.Name).ToArray();

    private static string[] Names(IEnumerable<MeshAsset> assets) =>
        assets.Select(asset => asset.Name).ToArray();

    [Fact]
    public void BuildsFolderTreeMirroringDisk()
    {
        CreateFile(@"Models\Props\barrel.obj");
        CreateFile(@"Models\tree.obj");
        Directory.CreateDirectory(Path.Combine(_root, "Empty"));

        var library = new AssetLibrary();
        var found = library.Discover(_root);

        Assert.Equal(2, found);
        Assert.NotNull(library.Root);
        Assert.Equal(Path.GetFileName(_root), library.Root!.Name);
        Assert.Equal(["Empty", "Models"], Names(library.Root.Subfolders));

        var models = library.Root.Subfolders[1];
        Assert.Equal(["tree"], Names(models.Files));
        Assert.Equal(2, models.TotalFileCount);
        Assert.Equal(["Props"], Names(models.Subfolders));
        Assert.Equal(["barrel"], Names(models.Subfolders[0].Files));

        Assert.True(library.Root.Subfolders[0].IsEmpty);
    }

    [Fact]
    public void IgnoresFilesThatAreNotMeshes()
    {
        CreateFile(@"Models\cube.obj");
        CreateFile("notes.txt", "not a mesh");
        CreateFile(@"Models\readme.md", "also not a mesh");

        var library = new AssetLibrary();

        Assert.Equal(1, library.Discover(_root));
        Assert.Equal(["cube"], Names(library.Catalog));
    }

    [Fact]
    public void CatalogRecordsRelativePathAndSize()
    {
        CreateFile(@"Models\cube.obj");

        var library = new AssetLibrary();
        library.Discover(_root);

        var asset = Assert.Single(library.Catalog);
        Assert.Equal("Models/cube.obj", asset.RelativePath);
        Assert.True(asset.SizeInBytes > 0);
        Assert.False(asset.IsLoaded);
    }

    [Fact]
    public void TryGetAssetFindsDiscoveredFilesIgnoringCase()
    {
        var path = CreateFile(@"Models\cube.obj");

        var library = new AssetLibrary();
        library.Discover(_root);

        Assert.True(library.TryGetAsset(path.ToUpperInvariant(), out var asset));
        Assert.Equal("cube", asset.Name);

        Assert.False(library.TryGetAsset(Path.Combine(_root, "Models", "missing.obj"), out _));
        Assert.False(library.TryGetAsset(string.Empty, out _));
    }

    [Fact]
    public void RescanKeepsLoadedMeshesAttached()
    {
        CreateFile(@"Models\cube.obj");

        var library = new AssetLibrary();
        library.Discover(_root);

        var mesh = library.Load(library.Catalog[0]);
        Assert.True(library.Catalog[0].IsLoaded);

        library.Refresh();

        var rescanned = Assert.Single(library.Catalog);
        Assert.Same(mesh, rescanned.Mesh);
        Assert.True(rescanned.IsLoaded);

        // The mesh cache is keyed by path, so loading again is free.
        Assert.Same(mesh, library.LoadObj(rescanned.Path));
    }

    [Fact]
    public void MissingDirectoryYieldsEmptyLibrary()
    {
        var library = new AssetLibrary();
        var missing = Path.Combine(_root, "does-not-exist");

        Assert.Equal(0, library.Discover(missing));
        Assert.Null(library.Root);
        Assert.Empty(library.Catalog);
    }

    [Fact]
    public void FolderToStringShowsPathBelowRoot()
    {
        CreateFile(@"Models\Props\barrel.obj");

        var library = new AssetLibrary();
        library.Discover(_root);

        var props = library.Root!.Subfolders[0].Subfolders[0];
        Assert.Equal($"{library.Root.Name}/Models/Props", props.ToString());
    }

    [Fact]
    public void EmptyFoldersAreStillBrowsable()
    {
        Directory.CreateDirectory(Path.Combine(_root, "OnlyFolders", "Nested"));

        var library = new AssetLibrary();

        Assert.Equal(0, library.Discover(_root));
        Assert.NotNull(library.Root);

        var onlyFolders = Assert.Single(library.Root!.Subfolders);
        Assert.Equal("OnlyFolders", onlyFolders.Name);
        Assert.Equal("Nested", Assert.Single(onlyFolders.Subfolders).Name);
        Assert.Equal(0, library.Root.TotalFileCount);
    }
}
