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
    private double downloadSpeedInMBps;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormattedDownloadSize))]
    private long downloadedBytes;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FormattedDownloadSize))]
    private long totalBytes;

    public string FormattedDownloadSpeed => $"{DownloadSpeedInMBps:0.00} MB/s";

    /// <summary>
    /// Formatted "downloaded / total" size, e.g. "1.2 GiB / 6.5 GiB".
    /// Null until the total size is known.
    /// </summary>
    public string? FormattedDownloadSize =>
        TotalBytes > 0
            ? $"{Size.FormatBytes((ulong)Math.Max(DownloadedBytes, 0))} / {Size.FormatBytes((ulong)TotalBytes)}"
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
