using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Helper;

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
    private double downloadSpeedBytesPerSec;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormattedDownloadSize))]
    private long downloadedBytes;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormattedDownloadSize))]
    private long totalBytes;

    public string FormattedDownloadSpeed =>
        $"{Size.FormatBase10Bytes((long)Math.Max(DownloadSpeedBytesPerSec, 0))}/s";

    /// <summary>
    /// Formatted "downloaded / total" size, e.g. "1.2 GB / 6.5 GB".
    /// Null until the total size is known.
    /// </summary>
    public string? FormattedDownloadSize =>
        TotalBytes > 0
            ? $"{Size.FormatBase10Bytes(Math.Max(DownloadedBytes, 0))} / {Size.FormatBase10Bytes(TotalBytes)}"
            : null;

    public virtual bool IsProgressVisible => Value > 0 || IsIndeterminate;
    public virtual bool IsTextVisible => !string.IsNullOrWhiteSpace(Text);

    public void ClearProgress()
    {
        Value = 0;
        Text = null;
        IsIndeterminate = false;
    }
}
