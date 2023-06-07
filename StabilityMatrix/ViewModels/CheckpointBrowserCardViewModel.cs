using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Extensions;
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
    public BitmapImage CardImage { get; init; }

    public override Visibility ProgressVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;
    public override Visibility TextVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;

    public CheckpointBrowserCardViewModel(CivitModel civitModel, IDownloadService downloadService, ISnackbarService snackbarService)
    {
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        CivitModel = civitModel;

        if (civitModel.ModelVersions.Any() && civitModel.ModelVersions[0].Images.Any())
        {
            CardImage = new BitmapImage(new Uri(civitModel.ModelVersions[0].Images[0].Url));
        }
        else
        {
            CardImage = new BitmapImage(
                new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
        }
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

        var latestVersion = model.ModelVersions[0];
        var latestModelFile = latestVersion.Files[0];
        var fileExpectedSha256 = latestModelFile.Hashes.SHA256;
        
        var downloadFolder = Path.Combine(SharedFolders.SharedFoldersPath,
            model.Type.ConvertTo<SharedFolderType>().GetStringValue());
        // Folders might be missing if user didn't install any packages yet
        Directory.CreateDirectory(downloadFolder);
        var downloadPath = Path.GetFullPath(Path.Combine(downloadFolder, latestModelFile.Name));

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
                snackbarService.ShowSnackbarAsync(
                    "This may be caused by network or server issues from CivitAI, please try again in a few minutes.",
                    "Download failed hash validation", LogLevel.Warning).SafeFireAndForget();
                return;
            }
            snackbarService.ShowSnackbarAsync($"{model.Type} {model.Name} imported successfully!",
                "Import complete", LogLevel.Trace).SafeFireAndForget();
        }
        
        IsIndeterminate = true;
        
        // Save connected model info
        var modelFileName = Path.GetFileNameWithoutExtension(latestModelFile.Name);
        var modelInfo = new ConnectedModelInfo(CivitModel, latestVersion, latestModelFile, DateTime.UtcNow);
        await modelInfo.SaveJsonToDirectory(downloadFolder, modelFileName);
        
        // If available, save a model image
        if (latestVersion.Images.Any())
        {
            var image = latestVersion.Images[0];
            var imageExtension = Path.GetExtension(image.Url).TrimStart('.');
            if (imageExtension is "jpg" or "jpeg" or "png")
            {
                var imageDownloadPath = Path.GetFullPath(Path.Combine(downloadFolder, $"{modelFileName}.preview.{imageExtension}"));
                await downloadService.DownloadToFileAsync(image.Url, imageDownloadPath);
            }
        }

        IsIndeterminate = false;
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
