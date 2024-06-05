using StabilityMatrix.Core.Helper;
using StabilityMatrix.Native;

namespace StabilityMatrix.Core;

public static class Test
{
    private static string tempFolder = string.Empty;

    public static void Main()
    {
        tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);

        // Test code here
        var numFiles = 1024;
        Test1(numFiles, true);
        Test2(numFiles, true);
        // Test3(numFiles, true);

        if (string.IsNullOrEmpty(tempFolder))
            return;

        DeleteDirectory(tempFolder);
    }

    private static void Test1(int numFiles, bool printResults)
    {
        var targetFiles = Enumerable
            .Range(0, numFiles)
            .Select(i => Path.Combine(tempFolder, $"{nameof(Test1)}_{i}_{Guid.NewGuid().ToString()}"))
            .ToArray();

        foreach (var targetFile in targetFiles)
        {
            File.Create(targetFile).Close();
        }

        if (printResults)
        {
            using (CodeTimer.StartNew($"System.IO.File.Delete\t({numFiles} files)"))
            {
                foreach (var targetFile in targetFiles)
                {
                    File.Delete(targetFile);
                }
            }
        }
        else
        {
            foreach (var targetFile in targetFiles)
            {
                File.Delete(targetFile);
            }
        }
    }

    private static void Test2(int numFiles, bool printResults)
    {
        var targetFiles = Enumerable
            .Range(0, numFiles)
            .Select(i => Path.Combine(tempFolder, $"{nameof(Test2)}_{i}_{Guid.NewGuid().ToString()}"))
            .ToArray();

        foreach (var targetFile in targetFiles)
        {
            File.Create(targetFile).Close();
        }

        if (printResults)
        {
            using (CodeTimer.StartNew($"NativeFileOperations.MoveFilesToRecyclebin\t({numFiles} files)"))
            {
                NativeFileOperations.RecycleBin!.MoveFilesToRecycleBin(targetFiles);
            }
        }
        else
        {
            NativeFileOperations.RecycleBin!.MoveFilesToRecycleBin(targetFiles);
        }
    }

    public static void DeleteDirectory(string directory)
    {
        // Enumerate to delete any directory links
        foreach (var item in Directory.EnumerateDirectories(directory))
        {
            var info = new DirectoryInfo(item);
            if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                info.Delete();
            }
            else
            {
                DeleteDirectory(item);
            }
        }
    }
}
