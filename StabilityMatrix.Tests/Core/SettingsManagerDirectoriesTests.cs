using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class SettingsManagerDirectoriesTests
{
    private DirectoryPath tempDir = null!;
    private DirectoryPath libraryDir = null!;
    private DirectoryPath originalAppDataHome = null!;

    [TestInitialize]
    public void Initialize()
    {
        tempDir = new DirectoryPath(Path.GetTempPath(), $"SMTest_{Guid.NewGuid():N}");
        libraryDir = tempDir.JoinDir("Library");
        libraryDir.Create();

        originalAppDataHome = Compat.AppDataHome;
        Compat.SetAppDataHome(tempDir.JoinDir("AppDataHome"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        Compat.SetAppDataHome(originalAppDataHome);
        tempDir.Delete(true);
    }

    private SettingsManager CreateWithLibrary()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance);
        settingsManager.SetLibraryDirOverride(libraryDir);

        Assert.IsTrue(settingsManager.TryFindLibrary());

        return settingsManager;
    }

    [TestMethod]
    public void TryFindLibrary_CreatesAppDirectories()
    {
        var settingsManager = CreateWithLibrary();

        Assert.IsTrue(Compat.AppDataHome.Exists);
        Assert.IsTrue(Compat.LogsDir.Exists);
        Assert.IsTrue(settingsManager.PackagesDirectory.Exists);
        Assert.IsTrue(settingsManager.WorkflowDirectory.Exists);
        Assert.IsTrue(settingsManager.ImagesDirectory.Exists);
        Assert.IsTrue(settingsManager.ExtensionPackDirectory.Exists);
        Assert.IsTrue(Directory.Exists(settingsManager.ModelsDirectory));
    }

    [TestMethod]
    public void TryFindLibrary_DoesNotCreateConsolidatedImagesDirectory()
    {
        // Subfolders of Images show as categories in the Outputs page
        var settingsManager = CreateWithLibrary();

        Assert.IsFalse(settingsManager.ConsolidatedImagesDirectory.Exists);
    }

    [TestMethod]
    public void TryFindLibrary_DoesNotCreateModelsDirectoryOverride()
    {
        var modelsOverride = tempDir.JoinDir("UnmountedDrive", "Models");
        libraryDir
            .JoinFile("settings.json")
            .WriteAllText(JsonSerializer.Serialize(new { ModelDirectoryOverride = modelsOverride.FullPath }));

        var settingsManager = CreateWithLibrary();

        Assert.AreEqual(modelsOverride.FullPath, settingsManager.ModelsDirectory);
        Assert.IsFalse(modelsOverride.Exists);
        Assert.IsFalse(libraryDir.JoinDir("Models").Exists);
    }

    [TestMethod]
    public void EnsureAppDirectories_RecreatesDeletedDirectory()
    {
        var settingsManager = CreateWithLibrary();
        settingsManager.PackagesDirectory.Delete(true);

        settingsManager.EnsureAppDirectories();
        settingsManager.EnsureAppDirectories();

        Assert.IsTrue(settingsManager.PackagesDirectory.Exists);
    }

    [TestMethod]
    public void EnsureAppDirectories_WithoutLibrary_OnlyCreatesAppDataDirectories()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance);

        settingsManager.EnsureAppDirectories();

        Assert.IsFalse(settingsManager.IsLibraryDirSet);
        Assert.IsTrue(Compat.LogsDir.Exists);
        Assert.IsFalse(libraryDir.JoinDir("Packages").Exists);
    }
}
