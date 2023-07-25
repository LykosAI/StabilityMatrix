using AsyncAwaitBestPractices;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Transaction object which saves settings manager changes when disposed.
/// </summary>
public class SettingsTransaction : IDisposable, IAsyncDisposable
{
    private readonly ISettingsManager settingsManager;
    private readonly Func<Task> onCommit;

    public Settings Settings => settingsManager.Settings;
    
    public SettingsTransaction(ISettingsManager settingsManager, Func<Task> onCommit)
    {
        this.settingsManager = settingsManager;
        this.onCommit = onCommit;
    }
    
    public void Dispose()
    {
        onCommit().SafeFireAndForget();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await onCommit();
        GC.SuppressFinalize(this);
    }
}
