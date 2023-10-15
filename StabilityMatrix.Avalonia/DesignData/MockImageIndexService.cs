using System.Threading.Tasks;
using DynamicData;
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
        InferenceImages = new IndexCollection<LocalImageFile, string>(
            this,
            file => file.AbsolutePath
        )
        {
            RelativePath = "Inference"
        };
    }

    /// <inheritdoc />
    public Task RefreshIndexForAllCollections()
    {
        return RefreshIndex(InferenceImages);
    }

    /// <inheritdoc />
    public Task RefreshIndex(IndexCollection<LocalImageFile, string> indexCollection)
    {
        var toAdd = new LocalImageFile[]
        {
            new()
            {
                AbsolutePath =
                    "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/4a7e00a7-6f18-42d4-87c0-10e792df2640/width=1152",
            },
            new()
            {
                AbsolutePath =
                    "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/a318ac1f-3ad0-48ac-98cc-79126febcc17/width=1024"
            },
            new()
            {
                AbsolutePath =
                    "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/16588c94-6595-4be9-8806-d7e6e22d198c/width=1152"
            }
        };

        indexCollection.ItemsSource.EditDiff(toAdd, LocalImageFile.Comparer);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex()
    {
        throw new System.NotImplementedException();
    }
}
