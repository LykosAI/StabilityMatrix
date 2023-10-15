using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockImageIndexService : IImageIndexService
{
    /// <inheritdoc />
    public IndexCollection<LocalImageFile, string> InferenceImages { get; } =
        new IndexCollection<LocalImageFile, string>(null!, file => file.RelativePath)
        {
            RelativePath = "inference"
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalImageFile>> GetLocalImagesByPrefix(string pathPrefix)
    {
        return Task.FromResult(
            (IReadOnlyList<LocalImageFile>)
                new LocalImageFile[]
                {
                    new()
                    {
                        RelativePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/4a7e00a7-6f18-42d4-87c0-10e792df2640/width=1152",
                        AbsolutePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/4a7e00a7-6f18-42d4-87c0-10e792df2640/width=1152",
                    },
                    new()
                    {
                        RelativePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/a318ac1f-3ad0-48ac-98cc-79126febcc17/width=1024",
                        AbsolutePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/a318ac1f-3ad0-48ac-98cc-79126febcc17/width=1024",
                    },
                    new()
                    {
                        RelativePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/16588c94-6595-4be9-8806-d7e6e22d198c/width=1152",
                        AbsolutePath =
                            "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/16588c94-6595-4be9-8806-d7e6e22d198c/width=1152",
                    }
                }
        );
    }

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

    /// <inheritdoc />
    public Task RemoveImage(LocalImageFile imageFile)
    {
        throw new System.NotImplementedException();
    }
}
