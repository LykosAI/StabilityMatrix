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
    /// Get all models of the specified type from the existing (in-memory) index.
    /// </summary>
    IEnumerable<LocalModelFile> GetFromModelIndex(SharedFolderType types);

    /// <summary>
    /// Get all models of the specified type from the existing index.
    /// </summary>
    Task<IReadOnlyList<LocalModelFile>> GetModelsOfType(SharedFolderType type);

    /// <summary>
    /// Starts a background task to refresh the local model file index.
    /// </summary>
    void BackgroundRefreshIndex();
}
