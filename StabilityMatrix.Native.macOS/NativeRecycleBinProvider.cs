using System.Diagnostics;
using JetBrains.Annotations;
using StabilityMatrix.Native.Abstractions;

namespace StabilityMatrix.Native.macOS;

[PublicAPI]
public class NativeRecycleBinProvider : INativeRecycleBinProvider
{
    /// <inheritdoc />
    public void MoveFileToRecycleBin(string path, NativeFileOperationFlags flags = default)
    {
        MoveFileToRecycleBinAsync(path, flags).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task MoveFileToRecycleBinAsync(string path, NativeFileOperationFlags flags = default)
    {
        await RunAppleScriptAsync($"tell application \\\"Finder\\\" to delete POSIX file \\\"{path}\\\"");
    }

    /// <inheritdoc />
    public void MoveFilesToRecycleBin(IEnumerable<string> paths, NativeFileOperationFlags flags = default)
    {
        MoveFilesToRecycleBinAsync(paths, flags).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task MoveFilesToRecycleBinAsync(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        var pathsArrayString = string.Join(", ", paths.Select(p => $"POSIX file \\\"{p}\\\""));

        await RunAppleScriptAsync($"tell application \\\"Finder\\\" to delete {{{pathsArrayString}}}");
    }

    /// <inheritdoc />
    public void MoveDirectoryToRecycleBin(string path, NativeFileOperationFlags flags = default)
    {
        MoveDirectoryToRecycleBinAsync(path, flags).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task MoveDirectoryToRecycleBinAsync(string path, NativeFileOperationFlags flags = default)
    {
        await RunAppleScriptAsync(
            $"tell application \\\"Finder\\\" to delete folder POSIX file \\\"{path}\\\""
        );
    }

    /// <inheritdoc />
    public void MoveDirectoriesToRecycleBin(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        MoveDirectoriesToRecycleBinAsync(paths, flags).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task MoveDirectoriesToRecycleBinAsync(
        IEnumerable<string> paths,
        NativeFileOperationFlags flags = default
    )
    {
        var pathsArrayString = string.Join(", ", paths.Select(p => $"folder POSIX file \\\"{p}\\\""));

        await RunAppleScriptAsync($"tell application \\\"Finder\\\" to delete {{{pathsArrayString}}}");
    }

    /// <summary>
    /// Runs an AppleScript script.
    /// </summary>
    private static async Task RunAppleScriptAsync(
        string script,
        CancellationToken cancellationToken = default
    )
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            Arguments = $"-e \"{script}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"The AppleScript script failed with exit code {process.ExitCode}: (StdOut = {stdOut}, StdErr = {stdErr})"
            );
        }
    }
}
