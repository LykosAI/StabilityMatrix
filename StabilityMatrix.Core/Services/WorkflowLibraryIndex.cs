using System.Collections.Concurrent;
using System.Text.Json;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Services;

public interface IWorkflowLibraryIndex
{
    /// <summary>
    /// CivitAI model versions currently in the workflow library, keyed by model id,
    /// derived from the metadata embedded in imported workflow files.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetInstalledCivitVersionsAsync();

    /// <summary>
    /// Library file paths imported from the given CivitAI model,
    /// optionally restricted to a single version.
    /// </summary>
    Task<IReadOnlyList<string>> GetInstalledFilesAsync(int modelId, int? versionId = null);
}

[RegisterSingleton<IWorkflowLibraryIndex, WorkflowLibraryIndex>]
public class WorkflowLibraryIndex : IWorkflowLibraryIndex
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsManager settingsManager;
    private readonly SemaphoreSlim scanLock = new(1, 1);
    private IReadOnlyDictionary<(int ModelId, int VersionId), List<string>>? cache;

    public WorkflowLibraryIndex(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;

        EventManager.Instance.WorkflowInstalled += (_, _) => cache = null;
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetInstalledCivitVersionsAsync()
    {
        var files = await GetCacheAsync().ConfigureAwait(false);

        var result = new Dictionary<int, IReadOnlySet<int>>();
        foreach (var group in files.Keys.GroupBy(key => key.ModelId))
        {
            result[group.Key] = group.Select(key => key.VersionId).ToHashSet();
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetInstalledFilesAsync(int modelId, int? versionId = null)
    {
        var files = await GetCacheAsync().ConfigureAwait(false);

        return files
            .Where(kv => kv.Key.ModelId == modelId && (versionId is null || kv.Key.VersionId == versionId))
            .SelectMany(kv => kv.Value)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(int ModelId, int VersionId), List<string>>> GetCacheAsync()
    {
        if (cache is { } cached)
            return cached;

        await scanLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return cache ??= await Task.Run(ScanLibrary).ConfigureAwait(false);
        }
        finally
        {
            scanLock.Release();
        }
    }

    private IReadOnlyDictionary<(int ModelId, int VersionId), List<string>> ScanLibrary()
    {
        var result = new Dictionary<(int, int), List<string>>();

        if (!Directory.Exists(settingsManager.WorkflowDirectory))
            return result;

        foreach (
            var workflowPath in LinkSafeFileSystem.EnumerateFiles(settingsManager.WorkflowDirectory, "*.json")
        )
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<WorkflowMetadata>(File.ReadAllText(workflowPath));

                // Import ids are "civitai-{modelId}-{versionId}-{name}"
                if (metadata?.Workflow?.Id?.Split('-') is not ["civitai", var modelPart, var versionPart, ..])
                    continue;

                if (
                    !int.TryParse(modelPart, out var modelId) || !int.TryParse(versionPart, out var versionId)
                )
                    continue;

                if (!result.TryGetValue((modelId, versionId), out var files))
                {
                    files = [];
                    result[(modelId, versionId)] = files;
                }

                files.Add(workflowPath);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Skipping unreadable workflow file {Path}", workflowPath);
            }
        }

        return result;
    }
}
