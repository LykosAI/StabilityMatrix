using System;
using System.IO;
using NCode.ReparsePoints;
using NLog;
using StabilityMatrix.Extensions;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.Packages;

namespace StabilityMatrix.Models;

public class SharedFolders : ISharedFolders
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsManager settingsManager;

    public SharedFolders(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }

    public void SetupLinksForPackage(BasePackage basePackage, string installPath)
    {
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders == null) return;

        var provider = ReparsePointFactory.Provider;
        foreach (var (folderType, relativePath) in sharedFolders)
        {
            var source = Path.Combine(settingsManager.ModelsDirectory, folderType.GetStringValue());
            var destination = Path.GetFullPath(Path.Combine(installPath, relativePath));
            // Create source folder if it doesn't exist
            if (!Directory.Exists(source))
            {
                Logger.Info($"Creating junction source {source}");
                Directory.CreateDirectory(source);
            }
            // Delete the destination folder if it exists
            if (Directory.Exists(destination))
            {
                // Copy all files from destination to source
                Logger.Info($"Copying files from {destination} to {source}");
                foreach (var file in Directory.GetFiles(destination))
                {
                    var fileName = Path.GetFileName(file);
                    var sourceFile = Path.Combine(source, fileName);
                    var destinationFile = Path.Combine(destination, fileName);
                    // Skip name collisions
                    if (File.Exists(sourceFile))
                    {
                        Logger.Warn($"Skipping file {fileName} because it already exists in {source}");
                        continue;
                    }
                    File.Move(destinationFile, sourceFile);
                }
                Logger.Info($"Deleting junction target {destination}");
                Directory.Delete(destination, true);
            }
            Logger.Info($"Creating junction link from {source} to {destination}");
            provider.CreateLink(destination, source, LinkType.Junction);
        }
    }
    
    /// <summary>
    /// Deletes junction links and remakes them. Unlike SetupLinksForPackage, 
    /// this will not copy files from the destination to the source.
    /// </summary>
    public void UpdateLinksForPackage(BasePackage basePackage, string installPath)
    {
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders == null) return;
        
        var provider = ReparsePointFactory.Provider;
        foreach (var (folderType, relativePath) in sharedFolders)
        {
            var source = Path.Combine(settingsManager.ModelsDirectory, folderType.GetStringValue());
            var destination = Path.GetFullPath(Path.Combine(installPath, relativePath));
            // Create source folder if it doesn't exist
            if (!Directory.Exists(source))
            {
                Logger.Info($"Creating junction source {source}");
                Directory.CreateDirectory(source);
            }
            // Delete the destination folder if it exists
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, false);
            }
            Logger.Info($"Updating junction link from {source} to {destination}");
            provider.CreateLink(destination, source, LinkType.Junction);
        }
    }

    public void RemoveLinksForPackage(BasePackage package, string installPath)
    {
        var sharedFolders = package.SharedFolders;
        if (sharedFolders == null)
        {
            return;
        }
        
        foreach (var (_, relativePath) in sharedFolders)
        {
            var destination = Path.GetFullPath(Path.Combine(installPath, relativePath));
            // Delete the destination folder if it exists
            if (Directory.Exists(destination))
            {
                Logger.Info($"Deleting junction target {destination}");
                Directory.Delete(destination, false);
            }
        }
    }
}
