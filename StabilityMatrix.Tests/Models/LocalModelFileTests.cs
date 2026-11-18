using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class LocalModelFileTests
{
    [TestMethod]
    public void Equals_ReturnsFalse_WhenEarlyAccessOnlyFlagDiffers()
    {
        var standardUpdateModel = CreateLocalModelFile(hasEarlyAccessUpdateOnly: false);
        var earlyAccessOnlyModel = standardUpdateModel with { HasEarlyAccessUpdateOnly = true };

        Assert.IsFalse(standardUpdateModel.Equals(earlyAccessOnlyModel));
        Assert.IsFalse(
            LocalModelFile.RelativePathConnectedModelInfoComparer.Equals(
                standardUpdateModel,
                earlyAccessOnlyModel
            )
        );
    }

    [TestMethod]
    public void RelativePathConnectedModelInfoComparer_TreatsEarlyAccessFlagAsDistinct()
    {
        var standardUpdateModel = CreateLocalModelFile(hasEarlyAccessUpdateOnly: false);
        var earlyAccessOnlyModel = standardUpdateModel with { HasEarlyAccessUpdateOnly = true };

        var set = new HashSet<LocalModelFile>(LocalModelFile.RelativePathConnectedModelInfoComparer)
        {
            standardUpdateModel,
            earlyAccessOnlyModel,
        };

        Assert.AreEqual(2, set.Count);
    }

    [TestMethod]
    public void GetDisplayPreviewImageFullPath_PrefersPreviewImageFile()
    {
        var rootModelDirectory = Path.Combine(Path.GetTempPath(), "Models");
        var model = CreateLocalModelFile(hasEarlyAccessUpdateOnly: false) with
        {
            PreviewImageRelativePath = Path.Combine("StableDiffusion", "model-a.preview.jpeg"),
            EmbeddedPreviewImageFullPath = Path.Combine(Path.GetTempPath(), "cover.jpeg"),
        };

        Assert.AreEqual(
            Path.Combine(rootModelDirectory, "StableDiffusion", "model-a.preview.jpeg"),
            model.GetDisplayPreviewImageFullPath(rootModelDirectory)
        );
    }

    [TestMethod]
    public void GetDisplayPreviewImageFullPath_FallsBackToEmbeddedCover()
    {
        var embeddedCoverPath = Path.Combine(Path.GetTempPath(), "cover.jpeg");
        var model = CreateLocalModelFile(hasEarlyAccessUpdateOnly: false) with
        {
            EmbeddedPreviewImageFullPath = embeddedCoverPath,
        };

        Assert.IsNull(model.GetPreviewImageFullPath(Path.GetTempPath()));
        Assert.AreEqual(embeddedCoverPath, model.GetDisplayPreviewImageFullPath(Path.GetTempPath()));
    }

    private static LocalModelFile CreateLocalModelFile(bool hasEarlyAccessUpdateOnly)
    {
        return new LocalModelFile
        {
            RelativePath = "StableDiffusion/model-a.safetensors",
            SharedFolderType = SharedFolderType.StableDiffusion,
            HasUpdate = true,
            HasEarlyAccessUpdateOnly = hasEarlyAccessUpdateOnly,
            ConnectedModelInfo = new ConnectedModelInfo
            {
                ModelId = 123,
                VersionId = 101,
                Source = ConnectedModelSource.Civitai,
                ModelName = "Model A",
                ModelDescription = string.Empty,
                VersionName = "v101",
                Tags = [],
                Hashes = new CivitFileHashes(),
            },
        };
    }
}
