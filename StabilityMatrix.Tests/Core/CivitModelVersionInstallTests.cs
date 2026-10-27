using System.Text.Json;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitModelVersionInstallTests
{
    [TestMethod]
    [DataRow("Model")]
    [DataRow("Pruned Model")]
    [DataRow("Diffusion Model")]
    [DataRow("UNet")]
    public void AnyMainVariantCounts_WithoutPrimaryOrDependencies(string fileType)
    {
        var version = JsonSerializer.Deserialize<CivitModelVersion>(
            $$$"""
            {"files":[
              {"type":"{{{fileType}}}","primary":false,"hashes":{"BLAKE3":"bf16"}},
              {"type":"Model","primary":true,"hashes":{"BLAKE3":"fp8"}},
              {"type":"Text Encoder","hashes":{"BLAKE3":"encoder"}},
              {"type":"VAE","hashes":{"BLAKE3":"vae"}}
            ]}
            """
        )!;

        Assert.IsTrue(version.IsInstalled(CivitModelType.Checkpoint, Hashes("bf16")));
        Assert.IsTrue(version.IsInstalled(CivitModelType.Checkpoint, Hashes("fp8")));
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, Hashes("encoder", "vae")));
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, Hashes()));
    }

    [TestMethod]
    [DataRow(CivitFileType.VAE)]
    [DataRow(CivitFileType.TextEncoder)]
    [DataRow(CivitFileType.VisionEncoder)]
    [DataRow(CivitFileType.CLIPVision)]
    [DataRow(CivitFileType.ControlNet)]
    [DataRow(CivitFileType.Upscaler)]
    [DataRow(CivitFileType.Negative)]
    [DataRow(CivitFileType.EnhancementLora)]
    [DataRow(CivitFileType.Unknown)]
    [DataRow(CivitFileType.Other)]
    [DataRow(CivitFileType.Config)]
    [DataRow(CivitFileType.Archive)]
    [DataRow(CivitFileType.TrainingData)]
    [DataRow(CivitFileType.Workflow)]
    public void SupportingOrUnclassifiedFile_DoesNotInstallCheckpoint_EvenIfPrimary(CivitFileType type)
    {
        var version = new CivitModelVersion
        {
            Files =
            [
                new CivitFile
                {
                    Type = type,
                    IsPrimary = true,
                    Hashes = new() { BLAKE3 = "component" },
                },
            ],
        };

        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, Hashes("component")));
    }

    [TestMethod]
    [DataRow(CivitModelType.VAE, CivitFileType.VAE)]
    [DataRow(CivitModelType.Upscaler, CivitFileType.Upscaler)]
    [DataRow(CivitModelType.Controlnet, CivitFileType.ControlNet)]
    [DataRow(CivitModelType.TextualInversion, CivitFileType.Negative)]
    [DataRow(CivitModelType.LORA, CivitFileType.EnhancementLora)]
    [DataRow(CivitModelType.LoCon, CivitFileType.EnhancementLora)]
    [DataRow(CivitModelType.DoRA, CivitFileType.EnhancementLora)]
    public void StandaloneArtifact_CountsForItsOwnModelType(CivitModelType modelType, CivitFileType fileType)
    {
        var version = new CivitModelVersion
        {
            Files =
            [
                new CivitFile
                {
                    Type = fileType,
                    Hashes = new() { BLAKE3 = "standalone" },
                },
            ],
        };

        Assert.IsTrue(version.IsInstalled(modelType, Hashes("standalone")));
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, Hashes("standalone")));
    }

    [TestMethod]
    [DataRow(CivitModelType.VAE)]
    [DataRow(CivitModelType.Upscaler)]
    [DataRow(CivitModelType.Controlnet)]
    [DataRow(CivitModelType.TextualInversion)]
    [DataRow(CivitModelType.LORA)]
    [DataRow(CivitModelType.LoCon)]
    [DataRow(CivitModelType.DoRA)]
    [DataRow(CivitModelType.Hypernetwork)]
    public void LegacyModelFile_CountsForStandaloneTypes(CivitModelType modelType)
    {
        var version = new CivitModelVersion
        {
            Files =
            [
                new CivitFile
                {
                    Type = CivitFileType.Model,
                    Hashes = new() { BLAKE3 = "model" },
                },
            ],
        };

        Assert.IsTrue(version.IsInstalled(modelType, Hashes("model")));
    }

    [TestMethod]
    public void MissingFilesAndInvalidHashes_DoNotCountAsInstalled()
    {
        var version = new CivitModelVersion();
        var hashes = Hashes("", " ", "valid");
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, hashes));
        version.Files = [];
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, hashes));
        version.Files =
        [
            null!,
            new CivitFile { Type = CivitFileType.Model },
            new CivitFile { Type = CivitFileType.Model, Hashes = new() },
            new CivitFile
            {
                Type = CivitFileType.Model,
                Hashes = new() { BLAKE3 = "" },
            },
            new CivitFile
            {
                Type = CivitFileType.Model,
                Hashes = new() { BLAKE3 = " " },
            },
        ];
        Assert.IsFalse(version.IsInstalled(CivitModelType.Checkpoint, hashes));
    }

    [TestMethod]
    public void InstalledHashComparison_UsesModelIndexComparer()
    {
        var version = new CivitModelVersion
        {
            Files =
            [
                new CivitFile
                {
                    Type = CivitFileType.Model,
                    Hashes = new() { BLAKE3 = "AABBCC" },
                },
            ],
        };

        Assert.IsTrue(version.IsInstalled(CivitModelType.Checkpoint, Hashes("aabbcc")));
    }

    private static HashSet<string> Hashes(params string[] hashes) =>
        new(hashes, StringComparer.OrdinalIgnoreCase);
}
