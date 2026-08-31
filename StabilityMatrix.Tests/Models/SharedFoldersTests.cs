using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class SharedFoldersTests
{
    private string tempFolder = string.Empty;
    private string TempModelsFolder => Path.Combine(tempFolder, "models");
    private string TempPackageFolder => Path.Combine(tempFolder, "package");

    private readonly Dictionary<SharedFolderType, string> sampleDefinitions = new()
    {
        [SharedFolderType.StableDiffusion] = "models/Stable-diffusion",
        [SharedFolderType.ESRGAN] = "models/ESRGAN",
        [SharedFolderType.Embeddings] = "embeddings",
    };

    [TestInitialize]
    public void Initialize()
    {
        tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (string.IsNullOrEmpty(tempFolder))
            return;
        TempFiles.DeleteDirectory(tempFolder);
    }

    private static readonly Dictionary<SharedFolderType, IReadOnlyList<string>> sampleLinkDefinitions = new()
    {
        [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
        [SharedFolderType.ESRGAN] = new[] { "models/ESRGAN" },
        [SharedFolderType.Embeddings] = new[] { "embeddings" },
    };

    private void CreateSampleJunctions()
    {
        SharedFolders
            .UpdateLinksForPackage(sampleLinkDefinitions, TempModelsFolder, TempPackageFolder)
            .GetAwaiter()
            .GetResult();
    }

    [TestMethod]
    public void SetupLinks_CreatesJunctions()
    {
        CreateSampleJunctions();

        // Check model folders
        foreach (var (folderType, relativePath) in sampleDefinitions)
        {
            var packagePath = Path.Combine(TempPackageFolder, relativePath);
            var modelFolder = Path.Combine(TempModelsFolder, folderType.GetStringValue());
            // Should exist and be a junction
            Assert.IsTrue(Directory.Exists(packagePath), $"Package folder {packagePath} does not exist.");
            var info = new DirectoryInfo(packagePath);
            Assert.IsTrue(
                info.Attributes.HasFlag(FileAttributes.ReparsePoint),
                $"Package folder {packagePath} is not a junction."
            );
            // Check junction target should be in models folder
            Assert.AreEqual(
                modelFolder,
                info.LinkTarget,
                $"Package folder {packagePath} does not point to {modelFolder}."
            );
        }
    }

    [TestMethod]
    public void SetupLinks_CanDeleteJunctions()
    {
        CreateSampleJunctions();

        var modelFolder = Path.Combine(
            tempFolder,
            "models",
            SharedFolderType.StableDiffusion.GetStringValue()
        );
        var packagePath = Path.Combine(
            tempFolder,
            "package",
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );

        // Write a file to a model folder
        File.Create(Path.Combine(modelFolder, "AFile")).Close();
        Assert.IsTrue(
            File.Exists(Path.Combine(modelFolder, "AFile")),
            $"File should exist in {modelFolder}."
        );
        // Should exist in the package folder
        Assert.IsTrue(
            File.Exists(Path.Combine(packagePath, "AFile")),
            $"File should exist in {packagePath}."
        );

        // Now delete the junction
        Directory.Delete(packagePath, false);
        Assert.IsFalse(Directory.Exists(packagePath), $"Package folder {packagePath} should not exist.");

        // The file should still exist in the model folder
        Assert.IsTrue(
            File.Exists(Path.Combine(modelFolder, "AFile")),
            $"File should exist in {modelFolder}."
        );
    }

    [TestMethod]
    public void SetupLinks_MovesExistingFilesToSharedFolder()
    {
        // Package has an existing non-empty model folder before links are set up
        var packageModelsPath = Path.Combine(
            TempPackageFolder,
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );
        Directory.CreateDirectory(packageModelsPath);
        File.Create(Path.Combine(packageModelsPath, "ExistingModel.safetensors")).Close();

        CreateSampleJunctions();

        // File should have been moved to the shared model folder, not deleted
        var modelFolder = Path.Combine(TempModelsFolder, SharedFolderType.StableDiffusion.GetStringValue());
        Assert.IsTrue(
            File.Exists(Path.Combine(modelFolder, "ExistingModel.safetensors")),
            $"File should have been moved to {modelFolder}."
        );

        // And should still be visible through the junction
        Assert.IsTrue(
            File.Exists(Path.Combine(packageModelsPath, "ExistingModel.safetensors")),
            $"File should be visible through the junction at {packageModelsPath}."
        );
    }

    [TestMethod]
    public void WouldMoveExistingFiles_EmptyPackage_ReturnsFalse()
    {
        Assert.IsFalse(
            SharedFolders.WouldMoveExistingFiles(sampleLinkDefinitions, TempModelsFolder, TempPackageFolder)
        );
    }

    [TestMethod]
    public void WouldMoveExistingFiles_NonEmptyModelFolder_ReturnsTrue()
    {
        var packageModelsPath = Path.Combine(
            TempPackageFolder,
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );
        Directory.CreateDirectory(packageModelsPath);
        File.Create(Path.Combine(packageModelsPath, "ExistingModel.safetensors")).Close();

        Assert.IsTrue(
            SharedFolders.WouldMoveExistingFiles(sampleLinkDefinitions, TempModelsFolder, TempPackageFolder)
        );
    }

    [TestMethod]
    public void WouldMoveExistingFiles_ExistingMatchingLinks_ReturnsFalse()
    {
        CreateSampleJunctions();

        // Links already point at the shared folders, so setup would be a no-op
        Assert.IsFalse(
            SharedFolders.WouldMoveExistingFiles(sampleLinkDefinitions, TempModelsFolder, TempPackageFolder)
        );
    }
}
