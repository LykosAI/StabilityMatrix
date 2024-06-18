using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Helper;

public static class SharedFoldersConfigHelper
{
    /// <summary>
    /// Updates a JSON object with shared folder layout rules, using the SourceTypes,
    /// converted to absolute paths using the sharedModelsDirectory.
    /// </summary>
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

        await UpdateJsonConfigFileAsync(
                layout,
                stream,
                rule =>
                    rule.SourceTypes.Select(
                        type => Path.Combine(sharedModelsDirectory, type.GetStringValue())
                    ),
                options
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a JSON object with shared folder layout rules, using the TargetRelativePaths,
    /// converted to absolute paths using the packageRootDirectory.
    /// </summary>
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

        await UpdateJsonConfigFileAsync(
                layout,
                stream,
                rule =>
                    rule.TargetRelativePaths.Select(NormalizePathSlashes)
                        .Select(path => Path.Combine(packageRootDirectory, path)),
                options
            )
            .ConfigureAwait(false);
    }

    private static async Task UpdateJsonConfigFileAsync(
        SharedFolderLayout layout,
        Stream configStream,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
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

        UpdateJsonConfig(layout, jsonNode, pathsSelector, options);

        configStream.Seek(0, SeekOrigin.Begin);
        configStream.SetLength(0);

        await JsonSerializer
            .SerializeAsync(configStream, jsonNode, options.JsonSerializerOptions)
            .ConfigureAwait(false);
    }

    private static void UpdateJsonConfig(
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
}
