using System;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(FirstLaunchSetupWindow))]
[ManagedService]
[Singleton]
public partial class FirstLaunchSetupViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool eulaAccepted;

    [ObservableProperty]
    private string gpuInfoText = string.Empty;

    [ObservableProperty]
    private RefreshBadgeViewModel checkHardwareBadge =
        new()
        {
            WorkingToolTipText = "We're checking some hardware specifications to determine compatibility.",
            SuccessToolTipText = "Everything looks good!",
            FailToolTipText =
                "We recommend a GPU with CUDA support for the best experience. "
                + "You can continue without one, but some packages may not work, and inference may be slower.",
            FailColorBrush = ThemeColors.ThemeYellow,
        };

    public FirstLaunchSetupViewModel()
    {
        CheckHardwareBadge.RefreshFunc = SetGpuInfo;
    }

    private async Task<bool> SetGpuInfo()
    {
        GpuInfo[] gpuInfo;

        await using (new MinimumDelay(800, 1200))
        {
            // Query GPU info
            gpuInfo = await Task.Run(() => HardwareHelper.IterGpuInfo().ToArray());
        }

        // First Nvidia GPU
        var activeGpu = gpuInfo.FirstOrDefault(
            gpu => gpu.Name?.Contains("nvidia", StringComparison.InvariantCultureIgnoreCase) ?? false
        );
        var isNvidia = activeGpu is not null;

        // Otherwise first GPU
        activeGpu ??= gpuInfo.FirstOrDefault();

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
}
