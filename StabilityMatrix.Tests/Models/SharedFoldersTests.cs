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

    private void CreateSampleJunctions()
    {
        var definitions = new Dictionary<SharedFolderType, IReadOnlyList<string>>
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
            [SharedFolderType.ESRGAN] = new[] { "models/ESRGAN" },
            [SharedFolderType.Embeddings] = new[] { "embeddings" },
        };
        SharedFolders
            .UpdateLinksForPackage(definitions, TempModelsFolder, TempPackageFolder)
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
    public void RemoveLinksForPackage_RemovesJunctions_KeepsTargetFiles()
    {
        CreateSampleJunctions();

        var modelFolder = Path.Combine(TempModelsFolder, SharedFolderType.StableDiffusion.GetStringValue());
        var packagePath = Path.Combine(
            TempPackageFolder,
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );

        File.Create(Path.Combine(modelFolder, "AFile")).Close();

        var definitions = new Dictionary<SharedFolderType, IReadOnlyList<string>>
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
        };
        SharedFolders.RemoveLinksForPackage(definitions, TempPackageFolder);

        Assert.IsFalse(Directory.Exists(packagePath), $"Junction {packagePath} should have been removed.");
        Assert.IsTrue(
            File.Exists(Path.Combine(modelFolder, "AFile")),
            $"File should still exist in {modelFolder}."
        );
    }

    [TestMethod]
    public void RemoveLinksForPackage_DoesNotDeleteRealDirectories()
    {
        // A real (non-junction) folder with user data at a shared folder path,
        // e.g. the models folder of a manually placed / imported package
        var packagePath = Path.Combine(
            TempPackageFolder,
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );
        Directory.CreateDirectory(packagePath);
        File.Create(Path.Combine(packagePath, "model.safetensors")).Close();

        var definitions = new Dictionary<SharedFolderType, IReadOnlyList<string>>
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
        };
        SharedFolders.RemoveLinksForPackage(definitions, TempPackageFolder);

        Assert.IsTrue(
            File.Exists(Path.Combine(packagePath, "model.safetensors")),
            $"User data in real directory {packagePath} must not be deleted."
        );
    }

    [TestMethod]
    public void RemoveLinksForPackage_DoesNotDeleteEmptyRealDirectories()
    {
        var packagePath = Path.Combine(
            TempPackageFolder,
            sampleDefinitions[SharedFolderType.StableDiffusion]
        );
        Directory.CreateDirectory(packagePath);

        var definitions = new Dictionary<SharedFolderType, IReadOnlyList<string>>
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
        };
        SharedFolders.RemoveLinksForPackage(definitions, TempPackageFolder);

        Assert.IsTrue(
            Directory.Exists(packagePath),
            $"Real directory {packagePath} must not be deleted, even when empty."
        );
    }
}
