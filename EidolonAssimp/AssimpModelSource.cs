using EidolonCore.Assets;

namespace Eidolon.Assimp;

/// <summary>
/// The importer as the scene loader sees it: a scene that contains an imported model asks for the file
/// again instead of storing its geometry.
/// </summary>
public sealed class AssimpModelSource : IModelImporter
{
    public ModelAsset? Import(string path) => AssimpModelImporter.Import(path);
}
