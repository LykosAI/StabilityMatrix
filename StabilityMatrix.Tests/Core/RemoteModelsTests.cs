using System.Reflection;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

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

        var resources = typeof(RemoteModels)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null))
            .SelectMany(
                value =>
                    value switch
                    {
                        IEnumerable<RemoteResource> remoteResources => remoteResources,
                        IEnumerable<HybridModelFile> hybridModels => hybridModels
                            .Where(model => model.DownloadableResource.HasValue)
                            .Select(model => model.DownloadableResource!.Value),
                        HybridModelFile { DownloadableResource: { } resource } => [resource],
                        _ => [],
                    }
            );

        var unpinnedModels = resources
            .Where(resource => resource.Url.Host == "huggingface.co")
            .Where(resource => executableExtensions.Contains(Path.GetExtension(resource.Url.AbsolutePath)))
            .Where(resource => resource.Url.AbsolutePath.Contains("/resolve/main/", StringComparison.Ordinal))
            .Select(resource => resource.Url)
            .ToArray();

        Assert.Empty(unpinnedModels);
    }
}
