using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Transaction object which saves settings manager changes when disposed.
/// </summary>
public class SettingsTransaction(ISettingsManager settingsManager, Action onCommit, Func<Task> onCommitAsync)
    : IDisposable,
        IAsyncDisposable
{
    public Settings Settings => settingsManager.Settings;

    public void Dispose()
    {
        onCommit();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await onCommitAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
