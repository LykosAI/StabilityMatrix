using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(FirstLaunchSetupWindow))]
[ManagedService]
[Singleton]
public partial class FirstLaunchSetupViewModel : DisposableViewModelBase
{
    [ObservableProperty]
    private bool eulaAccepted;

    [ObservableProperty]
    private string gpuInfoText = string.Empty;

    [ObservableProperty]
    private RefreshBadgeViewModel checkHardwareBadge =
        new()
        {
            WorkingToolTipText = Resources.Label_CheckingHardware,
            SuccessToolTipText = Resources.Label_EverythingLooksGood,
            FailToolTipText = Resources.Label_NvidiaGpuRecommended,
            FailColorBrush = ThemeColors.ThemeYellow,
        };

    [ObservableProperty]
    private bool selectDifferentGpu;

    [ObservableProperty]
    private ObservableCollection<GpuInfo> gpuInfoCollection = [];

    [ObservableProperty]
    private GpuInfo? selectedGpu;

    public string YouCanChangeThis =>
        string.Format(Resources.TextTemplate_YouCanChangeThisBehavior, "Settings > Idk Yet");

    public FirstLaunchSetupViewModel(ISettingsManager settingsManager)
    {
        CheckHardwareBadge.RefreshFunc = SetGpuInfo;

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.SelectedGpu,
                settings => settings.PreferredGpu,
                true
            )
        );
    }

    private async Task<bool> SetGpuInfo()
    {
        GpuInfo[] gpuInfo;

        await using (new MinimumDelay(800, 1200))
        {
            // Query GPU info
            gpuInfo = await Task.Run(() => HardwareHelper.IterGpuInfo().ToArray());
            GpuInfoCollection = new ObservableCollection<GpuInfo>(gpuInfo);
        }

        // First Nvidia GPU
        var activeGpu = gpuInfo.FirstOrDefault(
            gpu => gpu.Name?.Contains("nvidia", StringComparison.InvariantCultureIgnoreCase) ?? false
        );
        var isNvidia = activeGpu is not null;

        // Otherwise first GPU
        activeGpu ??= gpuInfo.FirstOrDefault();

        SelectedGpu = activeGpu;
        GpuInfoText = activeGpu is null
            ? "No GPU detected"
            : $"{activeGpu.Name} ({Size.FormatBytes(activeGpu.MemoryBytes)})";

        // Always succeed for macos arm
        if (Compat.IsMacOS && Compat.IsArm)
        {
            return true;
        }

        return isNvidia;
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        CheckHardwareBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }

    [RelayCommand]
    private void ToggleManualGpu()
    {
        SelectDifferentGpu = !SelectDifferentGpu;
    }
}
