using System.Threading;
using System.Threading.Tasks;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockSettingsManager : SettingsManager
{
    protected override void LoadSettings(CancellationToken cancellationToken = default) { }

    protected override Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    protected override void SaveSettings(CancellationToken cancellationToken = default) { }

    protected override Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
