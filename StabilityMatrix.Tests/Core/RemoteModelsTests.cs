using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Core;

public class RemoteModelsTests
{
    [Fact]
    public void ExecutableHuggingFaceModelsUsePinnedRevisions()
    {
        var executableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bin",
            ".ckpt",
            ".pt",
            ".pth",
        };

        var unpinnedModels = RemoteModels
            .UltralyticsModelFiles
            .Where(model => model.DownloadableResource?.Url.Host == "huggingface.co")
            .Where(model => executableExtensions.Contains(Path.GetExtension(model.DownloadableResource!.Value.Url.AbsolutePath)))
            .Where(model => model.DownloadableResource!.Value.Url.AbsolutePath.Contains("/resolve/main/", StringComparison.Ordinal))
            .Select(model => model.DownloadableResource!.Value.Url)
            .ToArray();

        Assert.Empty(unpinnedModels);
    }
}
