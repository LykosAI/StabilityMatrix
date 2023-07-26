using System.Globalization;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.ReparsePoints;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper;

public class SharedFolders : ISharedFolders
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;

    public SharedFolders(ISettingsManager settingsManager, IPackageFactory packageFactory)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
    }
    
    // Platform redirect for junctions / symlinks
    private static void CreateLinkOrJunction(string junctionDir, string targetDir, bool overwrite)
    {
        if (Compat.IsWindows)
        {
            Junction.Create(junctionDir, targetDir, overwrite);
        }
        else
        {
            // Create parent directory if it doesn't exist, since CreateSymbolicLink doesn't seem to
            new DirectoryPath(junctionDir).Parent?.Create();
            Directory.CreateSymbolicLink(junctionDir, targetDir);
        }
    }

    public static void SetupLinks(Dictionary<SharedFolderType, string> definitions, 
        DirectoryPath modelsDirectory, DirectoryPath installDirectory)
    {
        foreach (var (folderType, relativePath) in definitions)
        {
            var sourceDir = new DirectoryPath(modelsDirectory, folderType.GetStringValue());
            var destinationDir = new DirectoryPath(installDirectory, relativePath);
            // Create source folder if it doesn't exist
            if (!sourceDir.Exists)
            {
                Logger.Info($"Creating junction source {sourceDir}");
                sourceDir.Create();
            }
            // Delete the destination folder if it exists
            if (destinationDir.Exists)
            {
                // Copy all files from destination to source
                Logger.Info($"Copying files from {destinationDir} to {sourceDir}");
                foreach (var file in destinationDir.Info.EnumerateFiles())
                {
                    var sourceFile = sourceDir + file;
                    var destinationFile = destinationDir + file;
                    // Skip name collisions
                    if (File.Exists(sourceFile))
                    {
                        Logger.Warn($"Skipping file {file.FullName} because it already exists in {sourceDir}");
                        continue;
                    }
                    destinationFile.Info.MoveTo(sourceFile);
                }
                Logger.Info($"Deleting junction target {destinationDir}");
                destinationDir.Delete(true);
            }
            Logger.Info($"Creating junction link from {sourceDir} to {destinationDir}");
            CreateLinkOrJunction(destinationDir, sourceDir, true);
        }
    }

    public void SetupLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory)
    {
        var modelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders == null) return;
        SetupLinks(sharedFolders, modelsDirectory, installDirectory);
    }
    
    /// <summary>
    /// Deletes junction links and remakes them. Unlike SetupLinksForPackage, 
    /// this will not copy files from the destination to the source.
    /// </summary>
    public async Task UpdateLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory)
    {
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders is null) return;
        
        foreach (var (folderType, relativePath) in sharedFolders)
        {
            var sourceDir = new DirectoryPath(settingsManager.ModelsDirectory, folderType.GetStringValue());
            var destinationDir = installDirectory.JoinDir(relativePath);
            
            // Create source folder if it doesn't exist
            if (!sourceDir.Exists)
            {
                Logger.Info($"Creating junction source {sourceDir}");
                sourceDir.Create();
            }

            if (destinationDir.Exists)
            {
                // Existing dest is a link
                if (destinationDir.IsSymbolicLink)
                {
                    // If link is already the same, just skip
                    if (destinationDir.Info.LinkTarget == sourceDir)
                    {
                        Logger.Info($"Skipped updating matching folder link ({destinationDir} -> ({sourceDir})");
                        continue;
                    }
                    // Otherwise delete the link
                    Logger.Info($"Deleting existing junction at target {destinationDir}");
                    await destinationDir.DeleteAsync(false);
                }
                else
                {
                    // Move all files if not empty
                    if (destinationDir.Info.EnumerateFileSystemInfos().Any())
                    {
                        Logger.Info($"Moving files from {destinationDir} to {sourceDir}");
                        await FileTransfers.MoveAllFilesAndDirectories(
                            destinationDir,sourceDir, overwriteIfHashMatches: true);
                    }
                    
                    Logger.Info($"Deleting existing empty folder at target {destinationDir}");
                    await destinationDir.DeleteAsync(false);
                }
            }

            Logger.Info($"Updating junction link from {sourceDir} to {destinationDir}");
            CreateLinkOrJunction(destinationDir, sourceDir, true);
        }
    }

    private static void RemoveLinksForPackage(BasePackage package, DirectoryPath installPath)
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
            if (!Directory.Exists(destination)) continue;
            
            Logger.Info($"Deleting junction target {destination}");
            Directory.Delete(destination, false);
        }
    }

    public void RemoveLinksForAllPackages()
    {
        var libraryDir = settingsManager.LibraryDir;
        var packages = settingsManager.Settings.InstalledPackages;
        foreach (var package in packages)
        {
            if (package.PackageName == null) continue;
            var basePackage = packageFactory.FindPackageByName(package.PackageName);
            if (basePackage == null) continue;
            if (package.LibraryPath == null) continue;

            try
            {
                RemoveLinksForPackage(basePackage, Path.Combine(libraryDir, package.LibraryPath));
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to remove junction links for package {Package} " +
                            "({DisplayName}): {Message}", package.PackageName, package.DisplayName, e.Message);
            }
        }
    }
}
