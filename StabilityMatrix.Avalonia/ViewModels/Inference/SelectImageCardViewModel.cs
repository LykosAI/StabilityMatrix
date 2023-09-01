using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SelectImageCard))]
public partial class SelectImageCardViewModel : ViewModelBase
{
    [ObservableProperty]
    private ImageSource? imageSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionAvailable))]
    private bool isSelectionEnabled = true;

    public bool IsSelectionAvailable => IsSelectionEnabled && ImageSource == null;
}
