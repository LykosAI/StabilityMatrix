using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Services;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserCardViewModel : ProgressViewModel
{
    private readonly IDownloadService downloadService;
    private readonly ISnackbarService snackbarService;
    public CivitModel CivitModel { get; init; }

    public override Visibility ProgressVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;
    public override Visibility TextVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;

    public CheckpointBrowserCardViewModel(CivitModel civitModel, IDownloadService downloadService, ISnackbarService snackbarService)
    {
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        CivitModel = civitModel;
    }

    [RelayCommand]
    private void OpenModel()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"https://civitai.com/models/{CivitModel.Id}",
            UseShellExecute = true
        });
    }
    
    [RelayCommand]
    private async Task Import(CivitModel model)
    {
        Text = "Downloading...";

        var latestModelFile = model.ModelVersions[0].Files[0];
        var fileExpectedSha256 = latestModelFile.Hashes.SHA256;
        
        var downloadPath = Path.Combine(SharedFolders.SharedFoldersPath,
            SharedFolders.SharedFolderTypeToName(model.Type.ToSharedFolderType()), latestModelFile.Name);

        var downloadProgress = new Progress<ProgressReport>(progress =>
        {
            Value = progress.Percentage;
            Text = $"Importing... {progress.Percentage}%";
        });
        await downloadService.DownloadToFileAsync(latestModelFile.DownloadUrl, downloadPath, progress: downloadProgress);
        
        // When sha256 is available, validate the downloaded file
        if (!string.IsNullOrEmpty(fileExpectedSha256))
        {
            var hashProgress = new Progress<ProgressReport>(progress =>
            {
                Value = progress.Percentage;
                Text = $"Validating... {progress.Percentage}%";
            });
            var sha256 = await FileHash.GetSha256Async(downloadPath, hashProgress);
            if (sha256 != fileExpectedSha256.ToLowerInvariant())
            {
                Text = "Import Failed!";
                DelayedClearProgress(TimeSpan.FromSeconds(800));
                await snackbarService.ShowSnackbarAsync(
                    "This may be caused by network or server issues from CivitAI, please try again in a few minutes.",
                    "Download failed hash validation", LogLevel.Warning);
                return;
            }
            else
            {
                snackbarService.ShowSnackbarAsync($"{model.Type} {model.Name} imported successfully!",
                    "Import complete", LogLevel.Trace);
            }
        }
        
        Text = "Import complete!";
        Value = 100;
        DelayedClearProgress(TimeSpan.FromMilliseconds(800));
    }
    
    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay).ContinueWith(_ =>
        {
            Text = string.Empty;
            Value = 0;
        });
    }
}
