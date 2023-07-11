using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
/// Generic view model for progress reporting.
/// </summary>
public partial class ProgressViewModel : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTextVisible))]
    private string? text;

    [ObservableProperty]
    private string? description;
    
    [ObservableProperty]
    private double value;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private bool isProgressVisible;

    public virtual bool IsTextVisible => !string.IsNullOrWhiteSpace(Text);
}
