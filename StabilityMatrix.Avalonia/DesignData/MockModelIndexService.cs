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
    public IEnumerable<LocalModelFile> FindByModelType(SharedFolderType types)
    {
        return Array.Empty<LocalModelFile>();
    }

    /// <inheritdoc />
    public Task<Dictionary<SharedFolderType, LocalModelFolder>> FindAllFolders()
    {
        return Task.FromResult(new Dictionary<SharedFolderType, LocalModelFolder>());
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByModelTypeAsync(SharedFolderType type)
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

    public Task<bool> RemoveModelsAsync(IEnumerable<LocalModelFile> models)
    {
        return Task.FromResult(false);
    }

    public Task CheckModelsForUpdates()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex() { }
}
