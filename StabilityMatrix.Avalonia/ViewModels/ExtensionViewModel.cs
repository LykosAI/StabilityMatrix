using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class ExtensionViewModel() : ViewModelBase
{
    [ObservableProperty]
    private bool isSelected;

    public ExtensionBase Extension { get; init; }
}
