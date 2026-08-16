using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyUIWorkflowLinkTests
{
    private string tempDir = null!;
    private DirectoryPath workflowsDir = null!;
    private DirectoryPath installDir = null!;
    private ComfyUI comfy = null!;

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}");
        workflowsDir = new DirectoryPath(tempDir, "Workflows");
        installDir = new DirectoryPath(tempDir, "Packages", "ComfyUI");
        installDir.Create();

        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.WorkflowDirectory.Returns(workflowsDir);
        settingsManager.ModelsDirectory.Returns(Path.Combine(tempDir, "Models"));

        comfy = new ComfyUI(
            Substitute.For<IGithubApiCache>(),
            settingsManager,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            Substitute.For<IPipWheelService>(),
            Substitute.For<IRocmPackageHelper>()
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!Directory.Exists(tempDir))
            return;

        // Junction links must be unlinked (non-recursively) before the recursive delete,
        // which would otherwise fail or follow into the link target
        var links = new DirectoryInfo(tempDir)
            .EnumerateDirectories("*", SearchOption.AllDirectories)
            .Where(d => d.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .ToList();

        foreach (var link in links)
        {
            link.Attributes = FileAttributes.Normal;
            link.Delete(false);
        }

        Directory.Delete(tempDir, true);
    }

    [TestMethod]
    public async Task SetupModelFolders_LinksWorkflowLibraryIntoComfyUserDir()
    {
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        var linkDir = installDir.JoinDir("user", "default", "workflows", "Stability Matrix");
        Assert.IsTrue(linkDir.Exists, "Workflow library link was not created");
        Assert.IsTrue(linkDir.IsSymbolicLink, "Workflow library link is not a link");

        // Files written through the link land in the shared library
        await File.WriteAllTextAsync(linkDir.JoinFile("test.json"), "{}");
        Assert.IsTrue(File.Exists(workflowsDir.JoinFile("test.json")));
    }

    [TestMethod]
    public async Task SetupModelFolders_SkipsLinkWhenSharingDisabled()
    {
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.None);

        Assert.IsFalse(installDir.JoinDir("user", "default", "workflows", "Stability Matrix").Exists);
    }

    [TestMethod]
    public async Task RemoveModelFolderLinks_RemovesLinkButKeepsLibrary()
    {
        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        var linkDir = installDir.JoinDir("user", "default", "workflows", "Stability Matrix");
        await File.WriteAllTextAsync(linkDir.JoinFile("test.json"), "{}");

        await comfy.RemoveModelFolderLinks(installDir, SharedFolderMethod.Configuration);

        Assert.IsFalse(linkDir.Exists, "Link should be removed");
        Assert.IsTrue(
            File.Exists(workflowsDir.JoinFile("test.json")),
            "Shared library content should survive link removal"
        );
    }
}
