using System.Text.Json;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitFileTypeTests
{
    /// <summary>
    /// The canonical file type list from Civitai's <c>constants.modelFileTypes</c>
    /// (src/server/common/constants.ts). A type missing from our enum deserializes as
    /// Unknown, which hides the file (and its download link) in the model browser —
    /// keep <see cref="CivitFileType"/> in sync with this list.
    /// </summary>
    private static readonly string[] CivitaiModelFileTypes =
    [
        "Model",
        "Text Encoder",
        "Vision Encoder",
        "Pruned Model",
        "Negative",
        "Training Data",
        "VAE",
        "Config",
        "Archive",
        "UNet",
        "Diffusion Model",
        "CLIPVision",
        "ControlNet",
        "Workflow",
        "Upscaler",
        "Enhancement LoRA",
        "Other",
    ];

    [TestMethod]
    public void AllCivitaiFileTypes_ShouldDeserializeToKnownValues()
    {
        foreach (var typeString in CivitaiModelFileTypes)
        {
            var result = JsonSerializer.Deserialize<CivitFileType>($"\"{typeString}\"");

            Assert.AreNotEqual(
                CivitFileType.Unknown,
                result,
                $"'{typeString}' deserialized to Unknown - add a member (with EnumMember for spaced values) to CivitFileType"
            );
        }
    }

    [TestMethod]
    public void DiffusionModelFileTypes_ShouldCountAsModelWeights()
    {
        Assert.IsTrue(CivitFileType.Model.IsModelWeights());
        Assert.IsTrue(CivitFileType.PrunedModel.IsModelWeights());
        Assert.IsTrue(CivitFileType.DiffusionModel.IsModelWeights());
        Assert.IsTrue(CivitFileType.UNet.IsModelWeights());

        Assert.IsFalse(CivitFileType.VAE.IsModelWeights());
        Assert.IsFalse(CivitFileType.TrainingData.IsModelWeights());
        Assert.IsFalse(CivitFileType.Unknown.IsModelWeights());
    }

    [TestMethod]
    public void ExplicitlyTypedComponentFiles_ShouldMapToSharedFolders()
    {
        Assert.AreEqual(
            SharedFolderType.DiffusionModels,
            CivitFileType.DiffusionModel.GetExplicitSharedFolderType()
        );
        Assert.AreEqual(SharedFolderType.DiffusionModels, CivitFileType.UNet.GetExplicitSharedFolderType());
        Assert.AreEqual(SharedFolderType.VAE, CivitFileType.VAE.GetExplicitSharedFolderType());
        Assert.AreEqual(
            SharedFolderType.TextEncoders,
            CivitFileType.TextEncoder.GetExplicitSharedFolderType()
        );
        Assert.AreEqual(SharedFolderType.ClipVision, CivitFileType.CLIPVision.GetExplicitSharedFolderType());

        // Plain "Model" files depend on the parent model type, not the file type
        Assert.IsNull(CivitFileType.Model.GetExplicitSharedFolderType());
        Assert.IsNull(CivitFileType.PrunedModel.GetExplicitSharedFolderType());
    }
}
