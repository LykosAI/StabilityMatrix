using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AnalyticsSettingsPage))]
[Singleton, ManagedService]
public partial class AnalyticsSettingsViewModel : PageViewModelBase
{
    public override string Title => Resources.Label_Analytics;

    /// <inheritdoc />
    public override IconSource IconSource => new FASymbolIconSource { Symbol = @"fa-solid fa-chart-simple" };

    [ObservableProperty]
    private bool isPackageInstallAnalyticsEnabled;

    public AnalyticsSettingsViewModel(ISettingsManager settingsManager)
    {
        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsPackageInstallAnalyticsEnabled,
            s => s.OptedInToInstallTelemetry,
            true
        );
    }
}
