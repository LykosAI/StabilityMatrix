using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Services;

public interface IModelIndexService
{
    Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; }

    /// <summary>
    /// Set of all <see cref="ModelIndex"/> files Blake3 hashes.
    /// Synchronized with internal changes to <see cref="ModelIndex"/>.
    /// </summary>
    IReadOnlySet<string> ModelIndexBlake3Hashes { get; }

    /// <summary>
    /// Refreshes the local model file index.
    /// </summary>
    Task RefreshIndex();

    /// <summary>
    /// Starts a background task to refresh the local model file index.
    /// </summary>
    void BackgroundRefreshIndex();

    /// <summary>
    /// Get all models of the specified type from the existing (in-memory) index.
    /// </summary>
    IEnumerable<LocalModelFile> FindByModelType(SharedFolderType types);

    /// <summary>
    /// Gets all models in a hierarchical structure.
    /// </summary>
    Task<Dictionary<SharedFolderType, LocalModelFolder>> FindAllFolders();

    /// <summary>
    /// Find all models of the specified SharedFolderType.
    /// </summary>
    Task<IEnumerable<LocalModelFile>> FindByModelTypeAsync(SharedFolderType type);

    /// <summary>
    /// Find all models with the specified Blake3 hash.
    /// </summary>
    Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3);

    /// <summary>
    /// Find all models with the specified Sha256 hash
    /// </summary>
    Task<IEnumerable<LocalModelFile>> FindBySha256Async(string hashSha256);

    /// <summary>
    /// Remove a model from the index.
    /// </summary>
    Task<bool> RemoveModelAsync(LocalModelFile model);

    Task<bool> RemoveModelsAsync(IEnumerable<LocalModelFile> models);

    Task CheckModelsForUpdateAsync();
}
