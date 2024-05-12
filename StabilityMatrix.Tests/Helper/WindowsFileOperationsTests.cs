using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.ReparsePoints;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
[SupportedOSPlatform("windows")]
public class WindowsFileOperationsTests
{
    private string tempFolder = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Test cannot be run on anything but Windows currently.");
            return;
        }

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

    [TestMethod]
    public void FileOpDeleteItem_RecycleFile()
    {
        var targetFile = Path.Combine(tempFolder, $"RecycleFile_{Guid.NewGuid().ToString()}");
        File.Create(targetFile).Close();

        Assert.IsTrue(File.Exists(targetFile));

        using var fo = new WindowsFileOperations.FileOperation();

        fo.SetOperationFlags(WindowsFileOperations.FileOperationFlags.FOFX_RECYCLEONDELETE);
        fo.DeleteItem(targetFile);
        fo.PerformOperations();

        Assert.IsFalse(File.Exists(targetFile));
    }

    [TestMethod]
    public void FileOpDeleteItems_RecycleFiles()
    {
        var targetFiles = Enumerable
            .Range(0, 8)
            .Select(i => Path.Combine(tempFolder, $"RecycleFiles_{i}_{Guid.NewGuid().ToString()}"))
            .ToArray();

        foreach (var targetFile in targetFiles)
        {
            File.Create(targetFile).Close();
            Assert.IsTrue(File.Exists(targetFile));
        }

        using var fo = new WindowsFileOperations.FileOperation();

        fo.SetOperationFlags(WindowsFileOperations.FileOperationFlags.FOFX_RECYCLEONDELETE);
        fo.DeleteItems(targetFiles);
        fo.PerformOperations();

        foreach (var targetFile in targetFiles)
        {
            Assert.IsFalse(File.Exists(targetFile));
        }
    }
}
