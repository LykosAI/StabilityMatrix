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
        TempFiles.DeleteDirectory(tempDir);
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
    public async Task SetupModelFolders_LibraryLinkedToComfyWorkflowsDir_SkipsLink()
    {
        // The user pointed the library at ComfyUI's own workflows folder; linking it back
        // into that folder would create "Stability Matrix\Stability Matrix\..." forever
        var comfyWorkflows = installDir.JoinDir("user", "default", "workflows");
        comfyWorkflows.Create();
        await File.WriteAllTextAsync(comfyWorkflows.JoinFile("mine.json"), "{}");
        TempFiles.CreateDirectoryLink(workflowsDir, comfyWorkflows);

        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.IsFalse(
            comfyWorkflows.JoinDir("Stability Matrix").Exists,
            "Looping link should not be created"
        );
        Assert.IsTrue(File.Exists(comfyWorkflows.JoinFile("mine.json")), "User workflows must be untouched");
    }

    [TestMethod]
    public async Task SetupModelFolders_ExistingLoopingLink_IsRemoved()
    {
        // State left behind by an older version that created the link before the library was redirected
        var comfyWorkflows = installDir.JoinDir("user", "default", "workflows");
        comfyWorkflows.Create();
        await File.WriteAllTextAsync(comfyWorkflows.JoinFile("mine.json"), "{}");
        TempFiles.CreateDirectoryLink(workflowsDir, comfyWorkflows);
        var linkDir = comfyWorkflows.JoinDir("Stability Matrix");
        TempFiles.CreateDirectoryLink(linkDir, workflowsDir);

        await comfy.SetupModelFolders(installDir, SharedFolderMethod.Configuration);

        Assert.IsFalse(linkDir.Exists, "Looping link should be removed");
        Assert.IsTrue(File.Exists(comfyWorkflows.JoinFile("mine.json")), "User workflows must be untouched");
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
