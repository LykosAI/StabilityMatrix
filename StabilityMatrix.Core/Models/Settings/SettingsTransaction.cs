using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Transaction object which saves settings manager changes when disposed.
/// </summary>
public class SettingsTransaction(ISettingsManager settingsManager, Func<Task> onCommit)
    : IDisposable,
        IAsyncDisposable
{
    public Settings Settings => settingsManager.Settings;

    public void Dispose()
    {
        onCommit().SafeFireAndForget();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await onCommit().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
