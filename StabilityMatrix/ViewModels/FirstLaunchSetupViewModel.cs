using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Controls;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.ViewModels;

public partial class FirstLaunchSetupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool eulaAccepted;

    [ObservableProperty]
    private string gpuInfoText = string.Empty;

    [ObservableProperty]
    private RefreshBadgeViewModel checkHardwareBadge = new()
    {
        WorkingToolTipText = "We're checking some hardware specifications to determine compatibility.",
        SuccessToolTipText = "Everything looks good!",
        FailToolTipText = "We recommend a GPU with CUDA support for the best experience. " +
                          "You can continue without one, but some packages may not work, and inference may be slower.",
        FailColorBrush = AppBrushes.WarningYellow,
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
        var activeGpu = gpuInfo.FirstOrDefault(gpu => gpu.Name?.ToLowerInvariant().Contains("nvidia") ?? false);
        var isNvidia = activeGpu is not null;
        // Otherwise first GPU
        activeGpu ??= gpuInfo.FirstOrDefault();
        GpuInfoText = activeGpu is null
            ? "No GPU detected"
            : $"{activeGpu.Name} ({Size.FormatBytes(activeGpu.MemoryBytes)})";
        
        return isNvidia;
    }

    public void OnLoaded()
    {
        CheckHardwareBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }

    [RelayCommand]
    private void OpenLicenseLink()
    {
        ProcessRunner.OpenUrl("https://lykos.ai/matrix/license");
    }


}
