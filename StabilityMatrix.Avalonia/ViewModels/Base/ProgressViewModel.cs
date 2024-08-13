using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

/// <summary>
/// Generic view model for progress reporting.
/// </summary>
public partial class ProgressViewModel : DisposableViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTextVisible))]
    private string? text;

    [ObservableProperty]
    private string? description;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private double value;

    [ObservableProperty]
    private double maximum = 100;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private bool isIndeterminate;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormattedDownloadSpeed))]
    private double downloadSpeedInMBps;

    public string FormattedDownloadSpeed => $"{DownloadSpeedInMBps:0.00} MB/s";

    public virtual bool IsProgressVisible => Value > 0 || IsIndeterminate;
    public virtual bool IsTextVisible => !string.IsNullOrWhiteSpace(Text);

    public void ClearProgress()
    {
        Value = 0;
        Text = null;
        IsIndeterminate = false;
    }
}
