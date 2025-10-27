using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(CfzCudnnToggleCard))]
[ManagedService]
[RegisterTransient<CfzCudnnToggleCardViewModel>]
public partial class CfzCudnnToggleCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "CfzCudnnToggle";

    [ObservableProperty]
    private bool disableCudnn = true;
}
