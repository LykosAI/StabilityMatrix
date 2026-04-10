using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Reusable dialog view model for downloading missing models.
/// Can be configured for any provider that needs model downloads.
/// </summary>
[View(typeof(DownloadMissingModelsDialog))]
[ManagedService]
[RegisterTransient<DownloadMissingModelsViewModel>]
public partial class DownloadMissingModelsViewModel(
    ILogger<DownloadMissingModelsViewModel> logger,
    ISettingsManager settingsManager,
    ITrackedDownloadService trackedDownloadService,
    IDownloadService downloadService
) : ContentDialogViewModelBase
{
    /// <summary>
    /// Dialog title (e.g., "Flux Kontext Setup")
    /// </summary>
    [ObservableProperty]
    public partial string DialogTitle { get; set; } = "Download Required Models";

    /// <summary>
    /// Friendly description message
    /// </summary>
    [ObservableProperty]
    public partial string Description { get; set; } =
        "The following models are required. Select the ones you'd like to download.";

    /// <summary>
    /// Collection of downloadable model items
    /// </summary>
    public ObservableCollection<DownloadableModelItemViewModel> Models { get; } = [];

    /// <summary>
    /// Whether file sizes are being loaded
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoadingSizes { get; set; }

    /// <summary>
    /// Number of selected items
    /// </summary>
    public int SelectedCount => Models.Count(m => m.IsSelected);

    /// <summary>
    /// Total size of selected items
    /// </summary>
    public string TotalSelectedSizeText
    {
        get
        {
            var totalBytes = Models.Where(m => m.IsSelected).Sum(m => m.FileSize);
            return totalBytes > 0 ? Size.FormatBase10Bytes(totalBytes) : "Calculating...";
        }
    }

    /// <summary>
    /// Whether download can be started
    /// </summary>
    public bool CanStartDownload => SelectedCount > 0;

    /// <summary>
    /// The downloads that were started (populated after StartDownloadsAsync is called)
    /// </summary>
    public List<TrackedDownload> StartedDownloads { get; } = [];

    /// <summary>
    /// Set the models to display in the dialog
    /// </summary>
    public void SetModels(IEnumerable<RemoteResource> resources)
    {
        Models.Clear();

        foreach (var resource in resources)
        {
            var item = new DownloadableModelItemViewModel(resource);
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DownloadableModelItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(TotalSelectedSizeText));
                    OnPropertyChanged(nameof(CanStartDownload));
                }
            };
            Models.Add(item);
        }

        // Load file sizes asynchronously
        _ = LoadFileSizesAsync();
    }

    private async Task LoadFileSizesAsync()
    {
        if (Design.IsDesignMode)
            return;

        IsLoadingSizes = true;

        try
        {
            var tasks = Models.Select(async model =>
            {
                try
                {
                    if (model.Resource.Url is { } url)
                    {
                        var size = await downloadService.GetFileSizeAsync(url.ToString());
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            model.FileSize = size;
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get file size for {FileName}", model.FileName);
                }
            });

            await Task.WhenAll(tasks);
        }
        finally
        {
            IsLoadingSizes = false;
            OnPropertyChanged(nameof(TotalSelectedSizeText));
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var model in Models)
        {
            model.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var model in Models)
        {
            model.IsSelected = false;
        }
    }

    /// <summary>
    /// Queue downloads for all selected models. Returns the list of started downloads.
    /// Call this after dialog closes with Primary result.
    /// </summary>
    public async Task<List<TrackedDownload>> StartDownloadsAsync()
    {
        var selectedModels = Models.Where(m => m.IsSelected).ToList();
        StartedDownloads.Clear();

        if (selectedModels.Count == 0)
        {
            return StartedDownloads;
        }

        logger.LogInformation("Queueing download of {Count} models", selectedModels.Count);

        foreach (var model in selectedModels)
        {
            try
            {
                var download = await QueueDownloadAsync(model);
                if (download != null)
                {
                    StartedDownloads.Add(download);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to queue download for {FileName}", model.FileName);
            }
        }

        // Show progress flyout
        if (StartedDownloads.Count > 0)
        {
            EventManager.Instance.OnToggleProgressFlyout();
        }

        return StartedDownloads;
    }

    private async Task<TrackedDownload?> QueueDownloadAsync(DownloadableModelItemViewModel model)
    {
        var resource = model.Resource;

        var sharedFolderType =
            resource.ContextType as SharedFolderType?
            ?? throw new InvalidOperationException(
                $"ContextType is not SharedFolderType for {resource.FileName}"
            );

        var modelsDir = new DirectoryPath(settingsManager.ModelsDirectory).JoinDir(
            sharedFolderType.GetStringValue()
        );

        if (resource.RelativeDirectory is not null)
        {
            modelsDir = modelsDir.JoinDir(resource.RelativeDirectory);
        }

        // Ensure directory exists
        modelsDir.Create();

        var downloadPath = modelsDir.JoinFile(resource.FileName);

        logger.LogInformation("Queueing download: {FileName} to {Path}", resource.FileName, downloadPath);

        var download = trackedDownloadService.NewDownload(resource.Url, downloadPath);

        // Set hash for verification if available
        if (resource.HashSha256 is not null)
        {
            download.ExpectedHashSha256 = resource.HashSha256;
        }

        // Set extraction properties
        download.AutoExtractArchive = resource.AutoExtractArchive;
        download.ExtractRelativePath = resource.ExtractRelativePath;

        // Set context action for post-download processing
        download.ContextAction = new ModelPostDownloadContextAction();

        // Start the download
        await trackedDownloadService.TryStartDownload(download);

        return download;
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.Title = DialogTitle;
        dialog.Content = new DownloadMissingModelsDialog { DataContext = this };
        dialog.PrimaryButtonText = Resources.Action_Download;
        dialog.CloseButtonText = "Skip for Now";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.IsPrimaryButtonEnabled = CanStartDownload;
        dialog.MinDialogWidth = 550;

        return dialog;
    }
}
