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
}

[RegisterSingleton<IWorkflowLibraryIndex, WorkflowLibraryIndex>]
public class WorkflowLibraryIndex : IWorkflowLibraryIndex
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsManager settingsManager;
    private readonly SemaphoreSlim scanLock = new(1, 1);
    private IReadOnlyDictionary<int, IReadOnlySet<int>>? cache;

    public WorkflowLibraryIndex(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;

        EventManager.Instance.WorkflowInstalled += (_, _) => cache = null;
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetInstalledCivitVersionsAsync()
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

    private IReadOnlyDictionary<int, IReadOnlySet<int>> ScanLibrary()
    {
        var result = new Dictionary<int, IReadOnlySet<int>>();

        if (!Directory.Exists(settingsManager.WorkflowDirectory))
            return result;

        var versionsByModel = new Dictionary<int, HashSet<int>>();

        foreach (
            var workflowPath in Directory.EnumerateFiles(
                settingsManager.WorkflowDirectory,
                "*.json",
                SearchOption.AllDirectories
            )
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

                if (!versionsByModel.TryGetValue(modelId, out var versions))
                {
                    versions = [];
                    versionsByModel[modelId] = versions;
                }

                versions.Add(versionId);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Skipping unreadable workflow file {Path}", workflowPath);
            }
        }

        foreach (var (modelId, versions) in versionsByModel)
        {
            result[modelId] = versions;
        }

        return result;
    }
}
