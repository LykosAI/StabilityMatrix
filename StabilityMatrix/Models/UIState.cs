using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class UIState : ObservableObject
{
    [ObservableProperty]
    private bool? modelBrowserNsfwEnabled;
}
