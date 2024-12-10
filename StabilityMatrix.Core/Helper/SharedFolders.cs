using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.ReparsePoints;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper;

[RegisterSingleton<ISharedFolders, SharedFolders>]
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

    /// <summary>
    /// Platform redirect for junctions / symlinks
    /// </summary>
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

    /// <summary>
    /// Creates or updates junction link from the source to the destination.
    /// Moves destination files to source if they exist.
    /// </summary>
    /// <param name="sourceDir">Shared source (i.e. "Models/")</param>
    /// <param name="destinationDir">Destination (i.e. "webui/models/lora")</param>
    /// <param name="overwrite">Whether to overwrite the destination if it exists</param>
    /// <param name="recursiveDelete">Whether to recursively delete the directory after moving data out of it</param>
    public static async Task CreateOrUpdateLink(
        DirectoryPath sourceDir,
        DirectoryPath destinationDir,
        bool overwrite = false,
        bool recursiveDelete = false
    )
    {
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
                    return;
                }

                // Otherwise delete the link
                Logger.Info($"Deleting existing junction at target {destinationDir}");
                destinationDir.Info.Attributes = FileAttributes.Normal;
                await destinationDir.DeleteAsync(false).ConfigureAwait(false);
            }
            else
            {
                // Move all files if not empty
                if (destinationDir.Info.EnumerateFileSystemInfos().Any())
                {
                    Logger.Info($"Moving files from {destinationDir} to {sourceDir}");
                    await FileTransfers
                        .MoveAllFilesAndDirectories(
                            destinationDir,
                            sourceDir,
                            overwriteIfHashMatches: true,
                            overwrite: overwrite,
                            deleteSymlinks: true
                        )
                        .ConfigureAwait(false);
                }

                Logger.Info($"Deleting existing empty folder at target {destinationDir}");
                await destinationDir.DeleteAsync(recursiveDelete).ConfigureAwait(false);
            }
        }

        Logger.Info($"Updating junction link from {sourceDir} to {destinationDir}");
        CreateLinkOrJunction(destinationDir, sourceDir, true);
    }

    [Obsolete("Use static methods instead")]
    public void SetupLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory)
    {
        var modelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
        var sharedFolders = basePackage.SharedFolders;
        if (sharedFolders == null)
            return;
        UpdateLinksForPackage(sharedFolders, modelsDirectory, installDirectory).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Updates or creates shared links for a package.
    /// Will attempt to move files from the destination to the source if the destination is not empty.
    /// </summary>
    public static async Task UpdateLinksForPackage<T>(
        Dictionary<T, IReadOnlyList<string>> sharedFolders,
        DirectoryPath modelsDirectory,
        DirectoryPath installDirectory,
        bool recursiveDelete = false
    )
        where T : Enum
    {
        foreach (var (folderType, relativePaths) in sharedFolders)
        {
            foreach (var relativePath in relativePaths)
            {
                var sourceDir = new DirectoryPath(modelsDirectory, folderType.GetStringValue());
                var destinationDir = installDirectory.JoinDir(relativePath);

                // Check and remove destinationDir parent if it's a link
                if (destinationDir.Parent is { IsSymbolicLink: true } parentLink)
                {
                    Logger.Info("Deleting parent junction at target {Path}", parentLink.ToString());

                    await parentLink.DeleteAsync(false).ConfigureAwait(false);

                    // Recreate
                    parentLink.Create();
                }

                await CreateOrUpdateLink(sourceDir, destinationDir, recursiveDelete: recursiveDelete)
                    .ConfigureAwait(false);
            }
        }
    }

    public static void RemoveLinksForPackage<T>(
        Dictionary<T, IReadOnlyList<string>>? sharedFolders,
        DirectoryPath installPath
    )
        where T : Enum
    {
        if (sharedFolders == null)
        {
            return;
        }

        foreach (var (_, relativePaths) in sharedFolders)
        {
            foreach (var relativePath in relativePaths)
            {
                var destination = Path.GetFullPath(Path.Combine(installPath, relativePath));
                // Delete the destination folder if it exists
                if (!Directory.Exists(destination))
                    continue;

                Logger.Info($"Deleting junction target {destination}");
                Directory.Delete(destination, false);
            }
        }
    }

    public void RemoveLinksForAllPackages()
    {
        var packages = settingsManager.Settings.InstalledPackages;
        foreach (var package in packages)
        {
            try
            {
                if (
                    packageFactory.FindPackageByName(package.PackageName) is not { } basePackage
                    || package.FullPath is null
                )
                {
                    continue;
                }

                var sharedFolderMethod =
                    package.PreferredSharedFolderMethod ?? basePackage.RecommendedSharedFolderMethod;
                basePackage
                    .RemoveModelFolderLinks(package.FullPath, sharedFolderMethod)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception e)
            {
                Logger.Warn(
                    "Failed to remove links for package {Package} " + "({DisplayName}): {Message}",
                    package.PackageName,
                    package.DisplayName,
                    e.Message
                );
            }
        }
    }

    public static void SetupSharedModelFolders(DirectoryPath rootModelsDir)
    {
        if (string.IsNullOrWhiteSpace(rootModelsDir))
            return;

        Directory.CreateDirectory(rootModelsDir);
        var allSharedFolderTypes = Enum.GetValues<SharedFolderType>();
        foreach (var sharedFolder in allSharedFolderTypes)
        {
            if (sharedFolder == SharedFolderType.Unknown)
                continue;

            var dir = new DirectoryPath(rootModelsDir, sharedFolder.GetStringValue());
            dir.Create();

            if (sharedFolder == SharedFolderType.Ultralytics)
            {
                var bboxDir = new DirectoryPath(dir, "bbox");
                var segmDir = new DirectoryPath(dir, "segm");
                bboxDir.Create();
                segmDir.Create();
            }
        }
    }
}
