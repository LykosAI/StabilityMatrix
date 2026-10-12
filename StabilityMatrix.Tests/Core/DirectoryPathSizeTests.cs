using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.ReparsePoints;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class DirectoryPathSizeTests
{
    private string tempRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"sm-size-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempRoot))
            new DirectoryPath(tempRoot).DeleteVerbose();
    }

    /// <summary>
    /// A package using symlink sharing: models/checkpoints links to the shared models folder,
    /// one level below the package root, beside the package's own files and a hidden .git.
    /// </summary>
    private (DirectoryPath Package, string SharedModel) CreateLinkedPackage()
    {
        var shared = Directory.CreateDirectory(Path.Combine(tempRoot, "Models", "StableDiffusion"));
        var sharedModel = Path.Combine(shared.FullName, "model.safetensors");
        File.WriteAllBytes(sharedModel, new byte[10_000]);

        var package = Directory.CreateDirectory(Path.Combine(tempRoot, "Packages", "ComfyUI"));
        File.WriteAllBytes(Path.Combine(package.FullName, "main.py"), new byte[100]);

        var git = Directory.CreateDirectory(Path.Combine(package.FullName, ".git"));
        git.Attributes |= FileAttributes.Hidden;
        File.WriteAllBytes(Path.Combine(git.FullName, "pack"), new byte[20]);

        var models = Directory.CreateDirectory(Path.Combine(package.FullName, "models"));
        var link = Path.Combine(models.FullName, "checkpoints");
        if (Compat.IsWindows)
            Junction.Create(link, shared.FullName, false);
        else
            Directory.CreateSymbolicLink(link, shared.FullName);

        return (new DirectoryPath(package.FullName), sharedModel);
    }

    [TestMethod]
    public void GetSize_ExcludingLinks_SkipsNestedLinkTargets()
    {
        var (package, _) = CreateLinkedPackage();

        Assert.AreEqual(120, package.GetSize(includeSymbolicLinks: false));
    }

    [TestMethod]
    public void DeleteVerbose_RemovesNestedLinkButKeepsTarget()
    {
        var (package, sharedModel) = CreateLinkedPackage();

        package.DeleteVerbose();

        Assert.IsFalse(Directory.Exists(package.FullPath));
        Assert.IsTrue(File.Exists(sharedModel));
    }
}
