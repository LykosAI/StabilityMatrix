using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public static class FileTransfers
{
    public const int BufferSize = 10240;
    public static async Task CopyFiles(Dictionary<string, string> files, IProgress<ProgressReport>? fileProgress = default, IProgress<ProgressReport>? totalProgress = default)
    {
        var totalFiles = files.Count;
        var currentFiles = 0;
        var totalSize = Convert.ToUInt64(files.Keys.Select(x => new FileInfo(x).Length).Sum());
        var totalRead = 0ul;

        foreach(var (sourcePath, destPath) in files)
        {
            var totalReadForFile = 0ul;

            await using var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var inStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileSize = (ulong) inStream.Length;
            var fileName = Path.GetFileName(sourcePath);
            currentFiles++;
            await CopyStream(inStream , outStream, fileReadBytes =>
            {
                var lastRead = totalReadForFile;
                totalReadForFile = Convert.ToUInt64(fileReadBytes);
                totalRead += totalReadForFile - lastRead;
                fileProgress?.Report(new ProgressReport(totalReadForFile, fileSize, fileName, $"{currentFiles}/{totalFiles}"));
                totalProgress?.Report(new ProgressReport(totalRead, totalSize, fileName, $"{currentFiles}/{totalFiles}"));
            } );
        }
    }

    private static async Task CopyStream(Stream from, Stream to, Action<long> progress)
    {
        var buffer = new byte[BufferSize];
        var totalRead = 0L;

        while (totalRead < from.Length)
        {
            var read = await from.ReadAsync(buffer.AsMemory(0, BufferSize));
            await to.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;
            progress(totalRead);
        }
    }
}
