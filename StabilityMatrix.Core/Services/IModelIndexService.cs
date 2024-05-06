using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Services;

public interface IModelIndexService
{
    Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; }

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
    IEnumerable<LocalModelFile> GetFromModelIndex(SharedFolderType types);

    /// <summary>
    /// Gets all models in a hierarchical structure.
    /// </summary>
    Task<Dictionary<SharedFolderType, LocalModelFolder>> GetAllAsFolders();

    /// <summary>
    /// Find all models of the specified SharedFolderType.
    /// </summary>
    Task<IEnumerable<LocalModelFile>> FindAsync(SharedFolderType type);

    /// <summary>
    /// Find all models with the specified Blake3 hash.
    /// </summary>
    Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3);

    /// <summary>
    /// Remove a model from the index.
    /// </summary>
    Task<bool> RemoveModelAsync(LocalModelFile model);
}
