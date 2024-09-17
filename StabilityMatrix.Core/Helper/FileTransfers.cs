using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Helper;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class FileTransfers
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Determines suitable buffer size based on stream length.
    /// </summary>
    /// <param name="totalBytes"></param>
    /// <returns></returns>
    public static ulong GetBufferSize(ulong totalBytes) =>
        totalBytes switch
        {
            < Size.MiB => 8 * Size.KiB,
            < 100 * Size.MiB => 16 * Size.KiB,
            < 500 * Size.MiB => Size.MiB,
            < Size.GiB => 16 * Size.MiB,
            _ => 32 * Size.MiB
        };

    /// <summary>
    /// Copy all files and subfolders using a dictionary of source and destination file paths.
    /// Non-existing directories within the paths will be created.
    /// </summary>
    /// <param name="files">Dictionary of source and destination file paths</param>
    /// <param name="fileProgress">
    /// Optional (per file) progress
    /// <list type="bullet">
    /// <item>Current - Bytes read for file.</item>
    /// <item>Total - Size of file in bytes.</item>
    /// <item>Title - </item>
    /// </list>
    /// </param>
    /// <param name="totalProgress">
    /// Optional (total) progress.
    /// </param>
    public static async Task CopyFiles(
        Dictionary<string, string> files,
        IProgress<ProgressReport>? fileProgress = default,
        IProgress<ProgressReport>? totalProgress = default
    )
    {
        var totalFiles = files.Count;
        var completedFiles = 0;
        var totalSize = Convert.ToUInt64(files.Keys.Select(x => new FileInfo(x).Length).Sum());
        var totalRead = 0ul;

        foreach (var (sourcePath, destPath) in files)
        {
            var totalReadForFile = 0ul;

            await using var outStream = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read
            );
            await using var inStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            var fileSize = (ulong)inStream.Length;
            var fileName = Path.GetFileName(sourcePath);
            completedFiles++;
            var currentCompletedFiles = completedFiles;

            await CopyStream(
                    inStream,
                    outStream,
                    fileReadBytes =>
                    {
                        var lastRead = totalReadForFile;
                        totalReadForFile = Convert.ToUInt64(fileReadBytes);
                        totalRead += totalReadForFile - lastRead;
                        fileProgress?.Report(
                            new ProgressReport(
                                totalReadForFile,
                                fileSize,
                                fileName,
                                $"{currentCompletedFiles}/{totalFiles}"
                            )
                        );
                        totalProgress?.Report(
                            new ProgressReport(
                                totalRead,
                                totalSize,
                                fileName,
                                $"{currentCompletedFiles}/{totalFiles}"
                            )
                        );
                    }
                )
                .ConfigureAwait(false);
        }
    }

    private static async Task CopyStream(Stream from, Stream to, Action<long> progress)
    {
        var shared = ArrayPool<byte>.Shared;
        var bufferSize = (int)GetBufferSize((ulong)from.Length);
        var buffer = shared.Rent(bufferSize);
        var totalRead = 0L;

        try
        {
            while (totalRead < from.Length)
            {
                var read = await from.ReadAsync(buffer.AsMemory(0, bufferSize)).ConfigureAwait(false);
                await to.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                totalRead += read;
                progress(totalRead);
            }
        }
        finally
        {
            shared.Return(buffer);
        }
    }

    /// <summary>
    /// Move all files and sub-directories within the source directory to the destination directory.
    /// If the destination contains a file with the same name, we'll check if the hashes match.
    /// On matching hashes we skip the file, otherwise we throw an exception.
    /// </summary>
    /// <exception cref="FileTransferExistsException">
    /// If moving files results in name collision with different hashes.
    /// </exception>
    public static async Task MoveAllFilesAndDirectories(
        DirectoryPath sourceDir,
        DirectoryPath destinationDir,
        bool overwrite = false,
        bool overwriteIfHashMatches = false
    )
    {
        // Create the destination directory if it doesn't exist
        if (!destinationDir.Exists)
        {
            destinationDir.Create();
        }

        // First move files
        await MoveAllFiles(sourceDir, destinationDir, overwrite, overwriteIfHashMatches)
            .ConfigureAwait(false);

        // Then move directories
        foreach (var subDir in sourceDir.Info.EnumerateDirectories())
        {
            var destinationSubDir = destinationDir.JoinDir(subDir.Name);
            // Recursively move sub directories
            await MoveAllFilesAndDirectories(subDir, destinationSubDir, overwrite, overwriteIfHashMatches)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Move all files within the source directory to the destination directory.
    /// If the destination contains a file with the same name, we'll check if the hashes match.
    /// On matching hashes we skip the file, otherwise we throw an exception.
    /// </summary>
    /// <exception cref="FileTransferExistsException">
    /// If moving files results in name collision with different hashes.
    /// </exception>
    public static async Task MoveAllFiles(
        DirectoryPath sourceDir,
        DirectoryPath destinationDir,
        bool overwrite = false,
        bool overwriteIfHashMatches = false
    )
    {
        foreach (var file in sourceDir.Info.EnumerateFiles())
        {
            var sourceFile = sourceDir.JoinFile(file.Name);
            var destinationFile = destinationDir.JoinFile(file.Name);

            await MoveFileAsync(sourceFile, destinationFile, overwrite, overwriteIfHashMatches)
                .ConfigureAwait(false);
        }
    }

    public static async Task MoveFileAsync(
        FilePath sourceFile,
        FilePath destinationFile,
        bool overwrite = false,
        bool overwriteIfHashMatches = false
    )
    {
        if (destinationFile.Exists)
        {
            if (overwriteIfHashMatches)
            {
                // Check if files hashes are the same
                var sourceHash = await FileHash.GetBlake3Async(sourceFile).ConfigureAwait(false);
                var destinationHash = await FileHash.GetBlake3Async(destinationFile).ConfigureAwait(false);
                // For same hash, just delete original file
                if (sourceHash == destinationHash)
                {
                    Logger.Info(
                        $"Deleted source file {sourceFile.Name} as it already exists in {Path.GetDirectoryName(destinationFile)}."
                            + $" Matching Blake3 hash: {sourceHash}"
                    );
                    sourceFile.Delete();
                    return;
                }

                // append a number to the file name until it doesn't exist
                for (var i = 0; i < 100; i++)
                {
                    if (!destinationFile.Exists)
                        break;

                    destinationFile = new FilePath(
                        destinationFile.NameWithoutExtension + $" ({i})" + destinationFile.Extension
                    );
                }
            }
            else if (!overwrite)
            {
                throw new FileTransferExistsException(sourceFile, destinationFile);
            }
        }

        // Move the file
        await sourceFile.MoveToAsync(destinationFile).ConfigureAwait(false);
    }
}
