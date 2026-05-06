using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FileNameFormatProviderTests
{
    [TestMethod]
    public void TestFileNameFormatProviderValidate_Valid_ShouldNotThrow()
    {
        var provider = new FileNameFormatProvider();

        var result = provider.Validate("{date}_{time}-{model_name}-{seed}");
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    public void TestFileNameFormatProviderValidate_Invalid_ShouldThrow()
    {
        var provider = new FileNameFormatProvider();

        var result = provider.Validate("{date}_{time}-{model_name}-{seed}-{invalid}");
        Assert.AreNotEqual(ValidationResult.Success, result);

        Assert.AreEqual("Unknown variable 'invalid'", result.ErrorMessage);
    }

    [TestMethod]
    public void TestFileNameFormatProviderTryResolveVariable_UsesLocalModelMetadata()
    {
        var provider = new FileNameFormatProvider
        {
            LocalModelFile = new LocalModelFile
            {
                RelativePath = Path.Combine("Checkpoints", "local-file.safetensors"),
                SharedFolderType = SharedFolderType.StableDiffusion,
                ConnectedModelInfo = new ConnectedModelInfo
                {
                    ModelName = "Remote Model",
                    VersionName = "Version One",
                    ModelType = CivitModelType.Checkpoint,
                    AuthorUsername = "creator-name",
                    BaseModel = "SDXL",
                    RemoteFileName = "remote-file.safetensors",
                    RemoteFileId = 123,
                    Hashes = new CivitFileHashes(),
                },
            },
        };

        var resolved = provider.TryResolveVariable("file_name", out var fileName, out var error);

        Assert.IsTrue(resolved, error);
        Assert.AreEqual("remote-file", fileName);
    }

    [TestMethod]
    public void GetSampleForOrganization_ResolvesAllLocalOrganizationVariables()
    {
        var provider = FileNameFormatProvider.GetSampleForOrganization();

        foreach (var variable in FileNameFormatProvider.LocalOrganizationVariables)
        {
            var resolved = provider.TryResolveVariable(variable, out var value, out var error);

            Assert.IsTrue(resolved, $"Variable '{variable}' failed to resolve: {error}");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(value),
                $"Variable '{variable}' resolved to null or empty"
            );
        }
    }
}
