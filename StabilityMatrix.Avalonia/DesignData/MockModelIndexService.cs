using System;
using System.Collections.Generic;
using System.Linq;
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
    public IEnumerable<LocalModelFile> GetFromModelIndex(SharedFolderType types)
    {
        return Array.Empty<LocalModelFile>();
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindAsync(SharedFolderType type)
    {
        return Task.FromResult(Enumerable.Empty<LocalModelFile>());
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3)
    {
        return Task.FromResult(Enumerable.Empty<LocalModelFile>());
    }

    /// <inheritdoc />
    public Task<bool> RemoveModelAsync(LocalModelFile model)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex() { }
}
