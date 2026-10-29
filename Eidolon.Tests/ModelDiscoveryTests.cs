using EidolonCore.Assets;

namespace Eidolon.Tests;

public class ModelDiscoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eidolon-assets-" + Guid.NewGuid().ToString("N"));

    public ModelDiscoveryTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void ModelFilesAreListedButNeverParsedAsMeshes()
    {
        File.WriteAllText(Path.Combine(_directory, "hero.fbx"), "not really an fbx");
        File.WriteAllText(Path.Combine(_directory, "triangle.obj"), "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        var assets = new AssetLibrary();
        assets.Discover(_directory);

        var model = assets.Catalog.Single(asset => asset.Name == "hero");
        Assert.True(model.IsModel);
        Assert.False(model.IsLoaded);

        var mesh = assets.Catalog.Single(asset => asset.Name == "triangle");
        Assert.False(mesh.IsModel);

        // Loading a model as a mesh must fail with a message rather than trying to parse it.
        Assert.False(assets.TryLoad(model, out _, out var error));
        Assert.NotNull(error);
    }
}
