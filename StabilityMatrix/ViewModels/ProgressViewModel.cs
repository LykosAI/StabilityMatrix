using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.ViewModels;

/// <summary>
/// Generic view model for progress reporting.
/// </summary>
public partial class ProgressViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextVisibility))]
    private string text;
    
    [ObservableProperty]
    private double value;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    private bool isProgressVisible;
    
    public virtual Visibility ProgressVisibility => IsProgressVisible ? Visibility.Visible : Visibility.Collapsed;
    
    public virtual Visibility TextVisibility => string.IsNullOrEmpty(Text) ? Visibility.Collapsed : Visibility.Visible;
}
