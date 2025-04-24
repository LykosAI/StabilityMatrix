using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.Disposables.Internals;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockModelIndexService : IModelIndexService
{
    /// <inheritdoc />
    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; } = new();

    /// <inheritdoc />
    public IReadOnlySet<string> ModelIndexBlake3Hashes =>
        ModelIndex.Values.SelectMany(x => x).Select(x => x.HashBlake3).WhereNotNull().ToHashSet();

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

    public Task<IEnumerable<LocalModelFile>> FindBySha256Async(string hashSha256)
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

    public Task CheckModelsForUpdateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex() { }
}
