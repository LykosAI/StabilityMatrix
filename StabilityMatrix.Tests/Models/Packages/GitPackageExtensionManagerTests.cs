using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class GitPackageExtensionManagerTests
{
    private DirectoryPath libraryDir = null!;
    private DirectoryPath customNodesDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        libraryDir = new DirectoryPath(Path.GetTempPath(), $"SMTest_{Guid.NewGuid():N}");
        customNodesDir = libraryDir.JoinDir("Packages", "ComfyUI", "custom_nodes");
        customNodesDir.Create();
    }

    [TestCleanup]
    public void Cleanup()
    {
        libraryDir.Delete(true);
    }

    [TestMethod]
    public async Task TestGetInstalledExtensionsLiteAsync()
    {
        CreateGitExtension("ComfyUI-GGUF", "https://github.com/city96/ComfyUI-GGUF.git");
        // Registry installs unpack a zip - no git metadata, lowercased node id as folder name
        customNodesDir.JoinDir("comfyui-nunchaku").Create();
        customNodesDir.JoinDir("__pycache__").Create();
        customNodesDir.JoinDir(".disabled").Create();

        GlobalConfig.LibraryDir = libraryDir;
        var installedPackage = new InstalledPackage { LibraryPath = Path.Combine("Packages", "ComfyUI") };

        var extensions = (await new TestExtensionManager().GetInstalledExtensionsLiteAsync(installedPackage))
            .OrderBy(ext => ext.Title, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(2, extensions.Count, "Expected the git and the registry extension, nothing else");

        Assert.AreEqual("ComfyUI-GGUF", extensions[0].Title);
        Assert.AreEqual("https://github.com/city96/ComfyUI-GGUF.git", extensions[0].GitRepositoryUrl);

        Assert.AreEqual("comfyui-nunchaku", extensions[1].Title);
        Assert.IsNull(extensions[1].GitRepositoryUrl);

        Assert.IsNotNull(
            ExtensionMatcher.FindInstalled(
                ExtensionSpecifier.Parse("https://github.com/city96/ComfyUI-GGUF"),
                extensions
            )
        );
        Assert.IsNotNull(
            ExtensionMatcher.FindInstalled(
                ExtensionSpecifier.Parse("https://github.com/nunchaku-tech/ComfyUI-nunchaku"),
                extensions
            )
        );
    }

    private void CreateGitExtension(string folderName, string remoteUrl)
    {
        var gitDir = customNodesDir.JoinDir(folderName, ".git");
        gitDir.Create();
        File.WriteAllText(
            gitDir.JoinFile("config"),
            $"""
            [core]
            	repositoryformatversion = 0
            [remote "origin"]
            	url = {remoteUrl}
            	fetch = +refs/heads/*:refs/remotes/origin/*
            """
        );
    }

    private sealed class TestExtensionManager() : GitPackageExtensionManager(null!)
    {
        public override string RelativeInstallDirectory => "custom_nodes";

        public override Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
            ExtensionManifest manifest,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IEnumerable<PackageExtension>>([]);
    }
}
