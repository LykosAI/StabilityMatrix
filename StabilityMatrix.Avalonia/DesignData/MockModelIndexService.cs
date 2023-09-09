using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockModelIndexService : IModelIndexService
{
    /// <inheritdoc />
    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; } = new();

    /// <inheritdoc />
    public Task RefreshIndex()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalModelFile>> GetModelsOfType(SharedFolderType type)
    {
        return Task.FromResult<IReadOnlyList<LocalModelFile>>(new List<LocalModelFile>());
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex() { }
}
