using System.Runtime.InteropServices;
using StabilityMatrix.Native;
using StabilityMatrix.Native.Abstractions;

namespace StabilityMatrix.Tests.Native;

[TestClass]
public class NativeRecycleBinProviderTests
{
    private string tempFolder = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.IsFalse(NativeFileOperations.IsRecycleBinAvailable);
            Assert.IsNull(NativeFileOperations.RecycleBin);
            return;
        }

        Assert.IsTrue(NativeFileOperations.IsRecycleBinAvailable);
        Assert.IsNotNull(NativeFileOperations.RecycleBin);

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
    public void RecycleFile()
    {
        var targetFile = Path.Combine(tempFolder, $"{nameof(RecycleFile)}_{Guid.NewGuid().ToString()}");
        File.Create(targetFile).Close();

        Assert.IsTrue(File.Exists(targetFile));

        NativeFileOperations.RecycleBin!.MoveFileToRecycleBin(
            targetFile,
            NativeFileOperationFlags.Silent | NativeFileOperationFlags.NoConfirmation
        );

        Assert.IsFalse(File.Exists(targetFile));
    }

    [TestMethod]
    public void RecycleFiles()
    {
        var targetFiles = Enumerable
            .Range(0, 8)
            .Select(i => Path.Combine(tempFolder, $"{nameof(RecycleFiles)}_{i}_{Guid.NewGuid().ToString()}"))
            .ToArray();

        foreach (var targetFile in targetFiles)
        {
            File.Create(targetFile).Close();
            Assert.IsTrue(File.Exists(targetFile));
        }

        NativeFileOperations.RecycleBin!.MoveFilesToRecycleBin(
            targetFiles,
            NativeFileOperationFlags.Silent | NativeFileOperationFlags.NoConfirmation
        );

        foreach (var targetFile in targetFiles)
        {
            Assert.IsFalse(File.Exists(targetFile));
        }
    }

    [TestMethod]
    public void RecycleDirectory()
    {
        var targetDirectory = Path.Combine(
            tempFolder,
            $"{nameof(RecycleDirectory)}_{Guid.NewGuid().ToString()}"
        );
        Directory.CreateDirectory(targetDirectory);

        Assert.IsTrue(Directory.Exists(targetDirectory));

        NativeFileOperations.RecycleBin!.MoveDirectoryToRecycleBin(
            targetDirectory,
            NativeFileOperationFlags.Silent | NativeFileOperationFlags.NoConfirmation
        );

        Assert.IsFalse(Directory.Exists(targetDirectory));
    }

    [TestMethod]
    public void RecycleDirectories()
    {
        var targetDirectories = Enumerable
            .Range(0, 2)
            .Select(
                i => Path.Combine(tempFolder, $"{nameof(RecycleDirectories)}_{i}_{Guid.NewGuid().ToString()}")
            )
            .ToArray();

        foreach (var targetDirectory in targetDirectories)
        {
            Directory.CreateDirectory(targetDirectory);
            Assert.IsTrue(Directory.Exists(targetDirectory));
        }

        NativeFileOperations.RecycleBin!.MoveDirectoriesToRecycleBin(
            targetDirectories,
            NativeFileOperationFlags.Silent | NativeFileOperationFlags.NoConfirmation
        );

        foreach (var targetDirectory in targetDirectories)
        {
            Assert.IsFalse(Directory.Exists(targetDirectory));
        }
    }
}
