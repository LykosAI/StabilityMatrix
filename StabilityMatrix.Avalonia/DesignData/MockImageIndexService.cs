using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockImageIndexService : IImageIndexService
{
    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; } =
        new(null!, file => file.AbsolutePath) { RelativePath = "Inference" };

    /// <inheritdoc />
    public Task RefreshIndexForAllCollections()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RefreshIndex(IndexCollection<LocalImageFile, string> indexCollection)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        throw new System.NotImplementedException();
    }
}
