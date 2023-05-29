using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class LaunchOption : ObservableObject
{
    public string Name { get; set; }

    [ObservableProperty] private bool selected = false;
}
