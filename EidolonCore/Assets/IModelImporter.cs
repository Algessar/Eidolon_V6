namespace EidolonCore.Assets;

/// <summary>Turns a model file into a <see cref="ModelAsset"/>.</summary>
/// <remarks>
/// Loading a scene that contains an imported model has to read that file again, but the engine cannot
/// reference an importer. The contract lives here, the host supplies the implementation, and a load with
/// no importer simply leaves the imported entities without geometry.
/// </remarks>
public interface IModelImporter
{
    /// <summary>Imports the file, or returns null when it cannot be read.</summary>
    ModelAsset? Import(string path);
}
