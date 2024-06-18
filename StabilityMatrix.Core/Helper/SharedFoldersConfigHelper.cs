using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Helper;

public static class SharedFoldersConfigHelper
{
    /// <summary>
    /// Updates a JSON object with shared folder layout rules, using the SourceTypes,
    /// converted to absolute paths using the sharedModelsDirectory.
    /// </summary>
    public static void UpdateJsonConfigForShared(
        SharedFolderLayout layout,
        JsonObject jsonObject,
        string sharedModelsDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        UpdateJsonConfig(
            layout,
            jsonObject,
            rule =>
                rule.SourceTypes.Select(type => Path.Combine(sharedModelsDirectory, type.GetStringValue())),
            options
        );
    }

    public static async Task UpdateJsonConfigFileForSharedAsync(
        SharedFolderLayout layout,
        Stream configStream,
        string sharedModelsDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        options ??= SharedFoldersConfigOptions.Default;

        JsonObject jsonNode;

        if (configStream.Length == 0)
        {
            jsonNode = new JsonObject();
        }
        else
        {
            jsonNode =
                await JsonSerializer
                    .DeserializeAsync<JsonObject>(configStream, options.JsonSerializerOptions)
                    .ConfigureAwait(false) ?? new JsonObject();
        }

        UpdateJsonConfigForShared(layout, jsonNode, sharedModelsDirectory, options);

        configStream.Seek(0, SeekOrigin.Begin);
        configStream.SetLength(0);

        await JsonSerializer
            .SerializeAsync(configStream, jsonNode, options.JsonSerializerOptions)
            .ConfigureAwait(false);
    }

    public static async Task UpdateJsonConfigFileForSharedAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        string sharedModelsDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        var configPath = Path.Combine(
            packageRootDirectory,
            layout.RelativeConfigPath ?? throw new InvalidOperationException("RelativeConfigPath is null")
        );

        await using var stream = File.Open(configPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        await UpdateJsonConfigFileForSharedAsync(layout, stream, sharedModelsDirectory, options)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a JSON object with shared folder layout rules, using the TargetRelativePaths,
    /// converted to absolute paths using the packageRootDirectory.
    /// </summary>
    public static void UpdateJsonConfigForDefault(
        SharedFolderLayout layout,
        JsonObject jsonObject,
        string packageRootDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        UpdateJsonConfig(
            layout,
            jsonObject,
            rule =>
                rule.TargetRelativePaths.Select(NormalizePathSlashes)
                    .Select(path => Path.Combine(packageRootDirectory, path)),
            options
        );
    }

    public static async Task UpdateJsonConfigFileForDefaultAsync(
        SharedFolderLayout layout,
        Stream configStream,
        string packageRootDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        options ??= SharedFoldersConfigOptions.Default;

        JsonObject jsonNode;

        if (configStream.Length == 0)
        {
            jsonNode = new JsonObject();
        }
        else
        {
            jsonNode =
                await JsonSerializer
                    .DeserializeAsync<JsonObject>(configStream, options.JsonSerializerOptions)
                    .ConfigureAwait(false) ?? new JsonObject();
        }

        UpdateJsonConfigForDefault(layout, jsonNode, packageRootDirectory, options);

        configStream.Seek(0, SeekOrigin.Begin);
        configStream.SetLength(0);

        await JsonSerializer
            .SerializeAsync(configStream, jsonNode, options.JsonSerializerOptions)
            .ConfigureAwait(false);
    }

    public static async Task UpdateJsonConfigFileForDefaultAsync(
        SharedFolderLayout layout,
        string packageRootDirectory,
        SharedFoldersConfigOptions? options = null
    )
    {
        var configPath = Path.Combine(
            packageRootDirectory,
            layout.RelativeConfigPath ?? throw new InvalidOperationException("RelativeConfigPath is null")
        );

        await using var stream = File.Open(configPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        await UpdateJsonConfigFileForDefaultAsync(layout, stream, packageRootDirectory, options)
            .ConfigureAwait(false);
    }

    public static void UpdateJsonConfig(
        SharedFolderLayout layout,
        JsonObject jsonObject,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        SharedFoldersConfigOptions? options = null
    )
    {
        options ??= SharedFoldersConfigOptions.Default;

        var rulesByConfigPath = layout.GetRulesByConfigPath();

        foreach (var (configPath, rule) in rulesByConfigPath)
        {
            // Get paths to write with selector
            var paths = pathsSelector(rule).ToArray();

            // Multiple elements or alwaysWriteArray is true, write as array
            if (paths.Length > 1 || options.AlwaysWriteArray)
            {
                jsonObject[configPath] = new JsonArray(
                    paths.Select(path => (JsonNode)JsonValue.Create(path)).ToArray()
                );
            }
            // 1 element and alwaysWriteArray is false, write as string
            else if (paths.Length == 1)
            {
                jsonObject[configPath] = paths[0];
            }
            else
            {
                jsonObject.Remove(configPath);
            }
        }
    }

    private static string NormalizePathSlashes(string path)
    {
        if (Compat.IsWindows)
        {
            return path.Replace('/', '\\');
        }

        return path;
    }

    public class SharedFoldersConfigOptions
    {
        public bool AlwaysWriteArray { get; set; } = false;

        public static SharedFoldersConfigOptions Default => new();

        public JsonSerializerOptions JsonSerializerOptions =
            new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };
    }
}
