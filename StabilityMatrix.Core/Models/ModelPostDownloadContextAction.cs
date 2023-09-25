using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

public class ModelPostDownloadContextAction : IContextAction
{
    /// <inheritdoc />
    public object? Context { get; set; }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public void Invoke(IModelIndexService modelIndexService)
    {
        // Request reindex
        modelIndexService.BackgroundRefreshIndex();
    }
}
