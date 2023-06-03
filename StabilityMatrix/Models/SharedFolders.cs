using System;
using System.IO;
using NCode.ReparsePoints;
using NLog;

namespace StabilityMatrix.Models;

public class SharedFolders : ISharedFolders
{
    private const string SharedFoldersName = "Models";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public string SharedFoldersPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            SharedFoldersName);

    public string SharedFolderTypeToName(SharedFolderType folderType)
    {
        return Enum.GetName(typeof(SharedFolderType), folderType)!;
    }
    
    public void SetupLinksForPackage(BasePackage basePackage, string installPath)
    {
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders == null)
        {
            return;
        }
        
        var provider = ReparsePointFactory.Provider;
        foreach (var (folderType, relativePath) in sharedFolders)
        {
            var source = Path.GetFullPath(Path.Combine(SharedFoldersPath, SharedFolderTypeToName(folderType)));
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
}
