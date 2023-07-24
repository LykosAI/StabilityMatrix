using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

public class MockFirstLaunchSetupViewModel : FirstLaunchSetupViewModel
{
    public MockFirstLaunchSetupViewModel()
    {
        GpuInfoText = "Nvidia Geforce RTX 3090 (24.0 GiB)";
        CheckHardwareBadge.State = ProgressState.Working;
    }
}
