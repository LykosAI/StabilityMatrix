using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Packages.Config;

namespace StabilityMatrix.Core.Helper;

public static class SharedFoldersConfigHelper
{
    // Cache strategies to avoid repeated instantiation
    private static readonly Dictionary<ConfigFileType, IConfigSharingStrategy> Strategies =
        new()
        {
            { ConfigFileType.Json, new JsonConfigSharingStrategy() },
            { ConfigFileType.Yaml, new YamlConfigSharingStrategy() },
            { ConfigFileType.Fds, new FdsConfigSharingStrategy() }
            // Add more strategies here as needed
        };

    public static Task UpdateConfigFileForSharedAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        string sharedModelsDirectory,
        CancellationToken cancellationToken = default
    )
    {
        return UpdateConfigFileForSharedAsync(
            layout,
            packageRootDirectory,
            sharedModelsDirectory,
            layout.ConfigFileType ?? throw new InvalidOperationException("ConfigFileType is null"),
            layout.ConfigSharingOptions,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a config file with shared folder layout rules, using the SourceTypes,
    /// converted to absolute paths using the sharedModelsDirectory.
    /// </summary>
    public static async Task UpdateConfigFileForSharedAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        string sharedModelsDirectory,
        ConfigFileType fileType, // Specify the file type
        ConfigSharingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ConfigSharingOptions.Default;
        var configPath = Path.Combine(
            packageRootDirectory,
            layout.RelativeConfigPath ?? throw new InvalidOperationException("RelativeConfigPath is null")
        );
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!); // Ensure directory exists

        // Using FileStream ensures we handle file locking and async correctly
        await using var stream = new FileStream(
            configPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );

        if (!Strategies.TryGetValue(fileType, out var strategy))
        {
            throw new NotSupportedException($"Configuration file type '{fileType}' is not supported.");
        }

        await strategy
            .UpdateAndWriteAsync(
                stream,
                layout,
                rule =>
                {
                    // Handle Root, just use models directory (e.g., Swarm)
                    if (rule.IsRoot)
                    {
                        return [sharedModelsDirectory];
                    }

                    var paths = rule.SourceTypes.Select(type => type.GetStringValue()) // Get the enum string value (e.g., "StableDiffusion")
                        .Where(folderName => !string.IsNullOrEmpty(folderName)) // Filter out potentially empty mappings
                        .Select(folderName => Path.Combine(sharedModelsDirectory, folderName)); // Combine with base models dir

                    // If sub-path provided, add to all paths
                    if (!string.IsNullOrEmpty(rule.SourceSubPath))
                    {
                        paths = paths.Select(path => Path.Combine(path, rule.SourceSubPath));
                    }

                    return paths;
                },
                [],
                options,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public static Task UpdateConfigFileForDefaultAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        CancellationToken cancellationToken = default
    )
    {
        return UpdateConfigFileForDefaultAsync(
            layout,
            packageRootDirectory,
            layout.ConfigFileType ?? throw new InvalidOperationException("ConfigFileType is null"),
            layout.ConfigSharingOptions,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a config file with shared folder layout rules, using the TargetRelativePaths,
    /// converted to absolute paths using the packageRootDirectory (restores default paths).
    /// </summary>
    public static async Task UpdateConfigFileForDefaultAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        ConfigFileType fileType, // Specify the file type
        ConfigSharingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ConfigSharingOptions.Default;
        var configPath = Path.Combine(
            packageRootDirectory,
            layout.RelativeConfigPath ?? throw new InvalidOperationException("RelativeConfigPath is null")
        );
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!); // Ensure directory exists

        // Using FileStream ensures we handle file locking and async correctly
        await using var stream = new FileStream(
            configPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );

        if (!Strategies.TryGetValue(fileType, out var strategy))
        {
            throw new NotSupportedException($"Configuration file type '{fileType}' is not supported.");
        }

        var clearPaths = new List<string>();

        // If using clear root option, add the root key
        if (options.ConfigDefaultType is ConfigDefaultType.ClearRoot)
        {
            clearPaths.Add(options.RootKey ?? "");
        }

        await strategy
            .UpdateAndWriteAsync(
                stream,
                layout,
                rule =>
                    rule.TargetRelativePaths.Select(
                        path => Path.Combine(packageRootDirectory, NormalizePathSlashes(path))
                    ), // Combine relative with package root
                clearPaths,
                options,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    // Keep path normalization logic accessible if needed elsewhere, or inline it if only used here.
    private static string NormalizePathSlashes(string path)
    {
        // Replace forward slashes with backslashes on Windows, otherwise use forward slashes.
        return path.Replace(Compat.IsWindows ? '/' : '\\', Path.DirectorySeparatorChar);
    }
}
