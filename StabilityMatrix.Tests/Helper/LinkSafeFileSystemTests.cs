using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class LinkSafeFileSystemTests
{
    private string tempDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TempFiles.DeleteDirectory(tempDir);
    }

    private string CreateDir(params string[] segments)
    {
        var path = Path.Combine([tempDir, .. segments]);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateFile(params string[] segments)
    {
        var path = Path.Combine([tempDir, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return path;
    }

    [TestMethod]
    public void EnumerateFiles_LinkBackToRoot_YieldsEachFileOnce()
    {
        var root = CreateDir("root");
        CreateFile("root", "a.json");
        CreateFile("root", "sub", "b.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "sub", "loop"), root);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(root, "a.json"), Path.Combine(root, "sub", "b.json") },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_RootIsLinkAndChildLinksBackThroughIt_YieldsEachFileOnce()
    {
        // Data\Workflows -> ComfyUI\user\default\workflows, which contains "Stability Matrix" -> Data\Workflows
        var comfyWorkflows = CreateDir("Packages", "ComfyUI", "user", "default", "workflows");
        CreateFile("Packages", "ComfyUI", "user", "default", "workflows", "a.json");
        CreateFile("Packages", "ComfyUI", "user", "default", "workflows", "sub", "b.json");

        var library = Path.Combine(tempDir, "Workflows");
        TempFiles.CreateDirectoryLink(library, comfyWorkflows);
        TempFiles.CreateDirectoryLink(Path.Combine(comfyWorkflows, "Stability Matrix"), library);

        var files = LinkSafeFileSystem.EnumerateFiles(library, "*.json").ToList();

        // Paths stay rooted at the library path as given, so relative folders resolve against it
        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(library, "a.json"), Path.Combine(library, "sub", "b.json") },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_LinkToUnvisitedDirectory_IsFollowed()
    {
        var root = CreateDir("root");
        var elsewhere = CreateDir("elsewhere");
        CreateFile("elsewhere", "c.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "linked"), elsewhere);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(new[] { Path.Combine(root, "linked", "c.json") }, files);
    }

    [TestMethod]
    public void EnumerateFiles_TwoLinksToSameDirectory_VisitsItOnce()
    {
        var root = CreateDir("root");
        var elsewhere = CreateDir("elsewhere");
        CreateFile("elsewhere", "c.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "link1"), elsewhere);
        TempFiles.CreateDirectoryLink(Path.Combine(root, "link2"), elsewhere);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        Assert.AreEqual(1, files.Count);
    }

    [TestMethod]
    public void EnumerateFiles_DeeperThanMaxDepth_IsSkipped()
    {
        var root = CreateDir("root");
        CreateFile("root", "d0.json");
        CreateFile("root", "l1", "d1.json");
        CreateFile("root", "l1", "l2", "d2.json");

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json", maxDepth: 1).ToList();

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(root, "d0.json"), Path.Combine(root, "l1", "d1.json") },
            files
        );
    }

    [TestMethod]
    public void GetRealPath_ResolvesLinkChainsAndLinkedAncestors()
    {
        var real = CreateDir("real");
        CreateDir("real", "child");
        var link1 = Path.Combine(tempDir, "link1");
        var link2 = Path.Combine(tempDir, "link2");
        TempFiles.CreateDirectoryLink(link1, real);
        TempFiles.CreateDirectoryLink(link2, link1);

        Assert.AreEqual(real, LinkSafeFileSystem.GetRealPath(link2));
        Assert.AreEqual(
            Path.Combine(real, "child"),
            LinkSafeFileSystem.GetRealPath(Path.Combine(link2, "child"))
        );
    }

    [TestMethod]
    public void GetRealPath_PlainDirectory_IsUnchanged()
    {
        var real = CreateDir("real");

        Assert.AreEqual(real, LinkSafeFileSystem.GetRealPath(real));
        Assert.AreEqual(real, LinkSafeFileSystem.GetRealPath(real + Path.DirectorySeparatorChar));
    }

    [TestMethod]
    public void WouldLinkCycle_LinkDirectlyInsideSource_IsTrue()
    {
        var source = new DirectoryPath(CreateDir("source"));

        Assert.IsTrue(LinkSafeFileSystem.WouldLinkCycle(source, source.JoinDir("Stability Matrix")));
    }

    [TestMethod]
    public void WouldLinkCycle_LinkInsideSubfolderOfSource_IsTrue()
    {
        var source = new DirectoryPath(CreateDir("source"));
        CreateDir("source", "sub");

        Assert.IsTrue(LinkSafeFileSystem.WouldLinkCycle(source, source.JoinDir("sub", "Stability Matrix")));
    }

    [TestMethod]
    public void WouldLinkCycle_SourceIsLinkToLinkParent_IsTrue()
    {
        // The reported setup: the library is a link to ComfyUI's workflows folder,
        // and the link would be created inside that same folder
        var comfyWorkflows = CreateDir("Packages", "ComfyUI", "user", "default", "workflows");
        var library = new DirectoryPath(tempDir, "Workflows");
        TempFiles.CreateDirectoryLink(library, comfyWorkflows);

        var linkPath = new DirectoryPath(comfyWorkflows, "Stability Matrix");

        Assert.IsTrue(LinkSafeFileSystem.WouldLinkCycle(library, linkPath));
    }

    [TestMethod]
    public void WouldLinkCycle_LinkParentIsLinkToSource_IsTrue()
    {
        // The reverse setup: ComfyUI's workflows folder is a link to the library
        var library = new DirectoryPath(CreateDir("Workflows"));
        CreateDir("Packages", "ComfyUI", "user", "default");
        var comfyWorkflows = Path.Combine(tempDir, "Packages", "ComfyUI", "user", "default", "workflows");
        TempFiles.CreateDirectoryLink(comfyWorkflows, library);

        var linkPath = new DirectoryPath(comfyWorkflows, "Stability Matrix");

        Assert.IsTrue(LinkSafeFileSystem.WouldLinkCycle(library, linkPath));
    }

    [TestMethod]
    public void WouldLinkCycle_UnrelatedDirectories_IsFalse()
    {
        var source = new DirectoryPath(CreateDir("Workflows"));
        var comfyWorkflows = CreateDir("Packages", "ComfyUI", "user", "default", "workflows");

        Assert.IsFalse(
            LinkSafeFileSystem.WouldLinkCycle(source, new DirectoryPath(comfyWorkflows, "Stability Matrix"))
        );
    }

    [TestMethod]
    public void WouldLinkCycle_SourceInsideLinkParent_IsFalse()
    {
        // A library that lives below the link's parent is reachable twice but never loops
        var comfyWorkflows = CreateDir("Packages", "ComfyUI", "user", "default", "workflows");
        var source = new DirectoryPath(
            CreateDir("Packages", "ComfyUI", "user", "default", "workflows", "Library")
        );

        Assert.IsFalse(
            LinkSafeFileSystem.WouldLinkCycle(source, new DirectoryPath(comfyWorkflows, "Stability Matrix"))
        );
    }
}
