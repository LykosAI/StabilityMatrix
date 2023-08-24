using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Extensions;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class DirectoryPathExtensions
{
    /// <summary>
    /// Deletes a directory and all of its contents recursively.
    /// Uses Polly to retry the deletion if it fails, up to 5 times with an exponential backoff.
    /// </summary>
    public static Task DeleteVerboseAsync(this DirectoryPath directory, ILogger? logger = default)
    {
        var policy = Policy.Handle<IOException>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)),
                onRetry: (exception, calculatedWaitDuration) =>
                {
                    logger?.LogWarning(
                        exception,
                        "Deletion of {TargetDirectory} failed. Retrying in {CalculatedWaitDuration}",
                        directory, calculatedWaitDuration);
                });

        return policy.ExecuteAsync(async () =>
        {
            await Task.Run(() => { DeleteVerbose(directory, logger); });
        });
    }

    /// <summary>
    /// Deletes a directory and all of its contents recursively.
    /// Removes link targets without deleting the source.
    /// </summary>
    public static void DeleteVerbose(this DirectoryPath directory, ILogger? logger = default)
    {
        // Skip if directory does not exist
        if (!directory.Exists)
        {
            return;
        }
        // For junction points, delete with recursive false
        if (directory.IsSymbolicLink)
        {
            logger?.LogInformation("Removing junction point {TargetDirectory}", directory);
            try
            {
                directory.Delete(false);
                return;
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to delete junction point {directory}", ex);
            }
        }
        // Recursively delete all subdirectories
        foreach (var subDir in directory.Info.EnumerateDirectories())
        {
            DeleteVerbose(subDir, logger);
        }
        
        // Delete all files in the directory
        foreach (var filePath in directory.Info.EnumerateFiles())
        {
            try
            {
                filePath.Attributes = FileAttributes.Normal;
                filePath.Delete();
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to delete file {filePath.FullName}", ex);
            }
        }
        
        // Delete this directory
        try
        {
            directory.Delete(false);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to delete directory {directory}", ex);
        }
    }
}
