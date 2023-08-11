using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
/// Generic view model for progress reporting.
/// </summary>
public partial class ProgressViewModel : ViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTextVisible))]
    private string? text;

    [ObservableProperty]
    private string? description;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private double value;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private bool isIndeterminate;

    public virtual bool IsProgressVisible => Value > 0 || IsIndeterminate;
    public virtual bool IsTextVisible => !string.IsNullOrWhiteSpace(Text);
}
