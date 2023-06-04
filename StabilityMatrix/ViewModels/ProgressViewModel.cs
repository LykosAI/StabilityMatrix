using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.ViewModels;

/// <summary>
/// Generic view model for progress reporting.
/// </summary>
public partial class ProgressViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressTextVisibility))]
    private string progressText;
    
    [ObservableProperty]
    private double progressValue;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    private bool isProgressVisible;
    
    public Visibility ProgressVisibility => IsProgressVisible? Visibility.Visible : Visibility.Collapsed;
    
    public Visibility ProgressTextVisibility => string.IsNullOrEmpty(ProgressText) ? Visibility.Collapsed : Visibility.Visible;
}
