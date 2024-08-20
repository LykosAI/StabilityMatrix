using System.Threading.Tasks;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockImageIndexService : IImageIndexService
{
    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; }

    public MockImageIndexService()
    {
        InferenceImages = new IndexCollection<LocalImageFile, string>(this, file => file.AbsolutePath)
        {
            RelativePath = "Inference"
        };
    }

    /// <inheritdoc />
    public Task RefreshIndexForAllCollections()
    {
        return RefreshIndex(InferenceImages);
    }

    private static LocalImageFile GetSampleImage(string url)
    {
        return new LocalImageFile
        {
            AbsolutePath = url,
            GenerationParameters = GenerationParameters.GetSample(),
            ImageSize = new System.Drawing.Size(1024, 1024)
        };
    }

    /// <inheritdoc />
    public Task RefreshIndex(IndexCollection<LocalImageFile, string> indexCollection)
    {
        var toAdd = new[]
        {
            GetSampleImage(
                "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/a318ac1f-3ad0-48ac-98cc-79126febcc17/width=1024"
            ),
            GetSampleImage(
                "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/16588c94-6595-4be9-8806-d7e6e22d198c/width=1152"
            )
        };

        indexCollection.ItemsSource.EditDiff(toAdd);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        throw new System.NotImplementedException();
    }
}
