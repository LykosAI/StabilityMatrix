using System.Diagnostics;
using System.Text.Json;
using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

public class CivitPostDownloadContextAction : IContextAction
{
    /// <inheritdoc />
    public object? Context { get; set; }

    public static CivitPostDownloadContextAction FromCivitFile(CivitFile file)
    {
        return new CivitPostDownloadContextAction { Context = file.Hashes.BLAKE3 };
    }

    public void Invoke(ISettingsManager settingsManager, IModelIndexService modelIndexService)
    {
        var result = Context as string;

        if (Context is JsonElement jsonElement)
        {
            result = jsonElement.GetString();
        }

        if (result is null)
        {
            Debug.WriteLine($"Context {Context} is not a string.");
            return;
        }

        Debug.WriteLine($"Adding {result} to installed models.");
        settingsManager.Transaction(s =>
        {
            s.InstalledModelHashes ??= new HashSet<string>();
            s.InstalledModelHashes.Add(result);
        });

        // Also request reindex
        modelIndexService.BackgroundRefreshIndex();
    }
}
