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

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void EnumerateFiles_RealDirAlreadyClaimedAsLinkTarget_IsVisitedOnce(bool xyLinkIsDeeper)
    {
        // ext/X/Y/y.json is reachable two ways: through a link to ext/X/Y, and as a real subfolder
        // of a link to ext/X. Sibling enumeration order is file-system dependent, so the two rows
        // nest the links at different depths to swap which one is walked first; whichever it is,
        // the other must find the folder already scanned.
        var root = CreateDir("root");
        var sub = CreateDir("root", "sub");
        CreateFile("ext", "X", "Y", "y.json");

        var x = Path.Combine(tempDir, "ext", "X");
        var xy = Path.Combine(x, "Y");

        // One row puts the ext/X/Y link under sub, the other puts the ext/X link there
        var xyLink = Path.Combine(xyLinkIsDeeper ? sub : root, "inner");
        var xLink = Path.Combine(xyLinkIsDeeper ? root : sub, "outer");
        TempFiles.CreateDirectoryLink(xyLink, xy);
        TempFiles.CreateDirectoryLink(xLink, x);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        Assert.AreEqual(1, files.Count, $"Expected one file, got: {string.Join(", ", files)}");
    }

    [DataTestMethod]
    [DataRow("a_inner", "b_outer")]
    [DataRow("b_inner", "a_outer")]
    public void EnumerateFiles_NestedLinkTarget_SiblingLinkOrder_IsVisitedOnce(
        string innerName,
        string outerName
    )
    {
        if (!Compat.IsWindows)
        {
            Assert.Inconclusive(
                "Needs NTFS, which enumerates sibling directories in stored name order; "
                    + "EnumerateFiles_RealDirAlreadyClaimedAsLinkTarget_IsVisitedOnce covers the same "
                    + "bug portably by varying depth instead."
            );
            return;
        }

        // The maintainer's original repro: innerName -> ext/X/Y, outerName -> ext/X, so the inner
        // link's target is also reached as a real subfolder of the outer link. NTFS yields siblings
        // in name order, so the two rows walk the links in opposite orders.
        var root = CreateDir("root");
        CreateFile("ext", "X", "Y", "y.json");

        var x = Path.Combine(tempDir, "ext", "X");
        var xy = Path.Combine(x, "Y");

        TempFiles.CreateDirectoryLink(Path.Combine(root, innerName), xy);
        TempFiles.CreateDirectoryLink(Path.Combine(root, outerName), x);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        Assert.AreEqual(1, files.Count, $"Expected one file, got: {string.Join(", ", files)}");
    }

    [DataTestMethod]
    [DataRow("diffusion_models")]
    [DataRow("a_alias")]
    [DataRow("sub", "alias")]
    public void EnumerateFiles_RealFolderShadowedByLink_KeepsRealFolderPaths(params string[] linkSegments)
    {
        var root = CreateDir("root");
        CreateFile("root", "DiffusionModels", "a.json");
        CreateFile("root", "DiffusionModels", "b.json");

        var linkPath = Path.Combine([root, .. linkSegments]);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        TempFiles.CreateDirectoryLink(linkPath, Path.Combine(root, "DiffusionModels"));

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(root, "DiffusionModels", "a.json"),
                Path.Combine(root, "DiffusionModels", "b.json"),
            },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_JunctionTargetCaseMismatch_KeepsRealFolderPaths()
    {
        if (!Compat.IsWindows)
        {
            Assert.Inconclusive("Junctions with a differently-cased stored target are Windows-only.");
            return;
        }

        var root = CreateDir("root");
        CreateFile("root", "DiffusionModels", "a.json");
        CreateFile("root", "DiffusionModels", "b.json");

        // Store the junction target with different casing than the real folder on disk.
        TempFiles.CreateDirectoryLink(
            Path.Combine(root, "diffusion_models"),
            Path.Combine(root.ToUpperInvariant(), "DIFFUSIONMODELS")
        );

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(root, "DiffusionModels", "a.json"),
                Path.Combine(root, "DiffusionModels", "b.json"),
            },
            files
        );
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

    [DataTestMethod]
    [DataRow("a_alias")]
    [DataRow("z_alias")]
    public void EnumerateFiles_LinkedFolderShadowedByAliasOfLink_KeepsLinkFolderPaths(string aliasName)
    {
        // DiffusionModels is itself a link (user moved it to an external drive), and Swarm's
        // alias points at that link. The folder must still be listed under DiffusionModels.
        var root = CreateDir("root");
        CreateFile("ext", "diff", "a.json");
        CreateFile("ext", "diff", "b.json");
        var diffusion = Path.Combine(root, "DiffusionModels");
        TempFiles.CreateDirectoryLink(diffusion, Path.Combine(tempDir, "ext", "diff"));
        TempFiles.CreateDirectoryLink(Path.Combine(root, aliasName), diffusion);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(diffusion, "a.json"), Path.Combine(diffusion, "b.json") },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_DanglingLink_IsSkipped()
    {
        var root = CreateDir("root");
        CreateFile("root", "a.json");
        var gone = CreateDir("gone");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "dangling"), gone);
        Directory.Delete(gone);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(new[] { Path.Combine(root, "a.json") }, files);
    }

    [TestMethod]
    public void EnumerateFiles_RootGivenInDifferentCase_LinkToRealChild_IsVisitedOnce()
    {
        if (!Compat.IsWindows)
        {
            Assert.Inconclusive("Case-insensitive root spelling is Windows-only.");
            return;
        }

        var root = CreateDir("root");
        CreateFile("root", "DiffusionModels", "a.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "z_alias"), Path.Combine(root, "DiffusionModels"));

        // Caller spells the root differently from how it is stored on disk
        var spelledRoot = root.ToUpperInvariant();
        var files = LinkSafeFileSystem.EnumerateFiles(spelledRoot, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(spelledRoot, "DiffusionModels", "a.json") },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_LinkToAncestorOfRoot_YieldsEachFileOnce()
    {
        var root = CreateDir("root");
        CreateFile("root", "a.json");
        CreateFile("other", "o.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "up"), tempDir);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine(root, "a.json"), Path.Combine(root, "up", "other", "o.json") },
            files
        );
    }

    [TestMethod]
    public void EnumerateFiles_LinkInsideLinkTarget_IsFollowed()
    {
        var root = CreateDir("root");
        CreateDir("ext", "A");
        CreateFile("ext", "B", "b.json");
        TempFiles.CreateDirectoryLink(Path.Combine(root, "L1"), Path.Combine(tempDir, "ext", "A"));
        TempFiles.CreateDirectoryLink(
            Path.Combine(tempDir, "ext", "A", "toB"),
            Path.Combine(tempDir, "ext", "B")
        );

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(new[] { Path.Combine(root, "L1", "toB", "b.json") }, files);
    }

    [TestMethod]
    public void EnumerateFiles_TwoLinksLoopingIntoEachOther_AreSkipped()
    {
        var root = CreateDir("root");
        CreateFile("root", "a.json");
        // A link needs an existing target when created: make b real, link a -> b, then replace b
        // with a link back to a
        var a = Path.Combine(root, "a");
        var b = CreateDir("root", "b");
        TempFiles.CreateDirectoryLink(a, b);
        Directory.Delete(b);
        TempFiles.CreateDirectoryLink(b, a);

        var files = LinkSafeFileSystem.EnumerateFiles(root, "*.json").ToList();

        CollectionAssert.AreEquivalent(new[] { Path.Combine(root, "a.json") }, files);
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
