using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class BananaVisionModelCategorizationTests
{
    [TestMethod]
    public void GetModelTermMatch_OtherMetadataWithSecondaryFilename_ReturnsSecondary()
    {
        var model = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = Path.Combine("Lora", "my_flux_style.safetensors"),
                SharedFolderType = SharedFolderType.Lora,
                ConnectedModelInfo = new ConnectedModelInfo { BaseModel = "Other" },
            }
        );

        var match = BananaVisionPageViewModel.GetModelTermMatch(model, ["Kontext"], ["Flux"]);

        Assert.AreEqual(BananaVisionModelTermMatch.Secondary, match);
    }

    [TestMethod]
    public void GetModelTermMatch_KnownDifferentMetadataWithoutFilenameMatch_IsExcluded()
    {
        var model = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = Path.Combine("Lora", "watercolor_style.safetensors"),
                SharedFolderType = SharedFolderType.Lora,
                ConnectedModelInfo = new ConnectedModelInfo { BaseModel = "SDXL 1.0" },
            }
        );

        var match = BananaVisionPageViewModel.GetModelTermMatch(model, ["Klein", "Flux.2"], ["Flux"]);

        Assert.AreEqual(BananaVisionModelTermMatch.Excluded, match);
    }
}
