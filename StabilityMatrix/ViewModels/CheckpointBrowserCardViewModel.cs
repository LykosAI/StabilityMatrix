using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Refit;
using StabilityMatrix.Extensions;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Models.Progress;
using StabilityMatrix.Services;
using Wpf.Ui.Controls;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserCardViewModel : ProgressViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IDownloadService downloadService;
    private readonly ISnackbarService snackbarService;
    private readonly ISettingsManager settingsManager;
    public CivitModel CivitModel { get; init; }
    public BitmapImage CardImage { get; set; }

    public override Visibility ProgressVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;
    public override Visibility TextVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;

    public CheckpointBrowserCardViewModel(CivitModel civitModel, IDownloadService downloadService, ISnackbarService snackbarService, ISettingsManager settingsManager)
    {
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        this.settingsManager = settingsManager;
        CivitModel = civitModel;
        CardImage = GetImage();
        
        this.settingsManager.ModelBrowserNsfwEnabledChanged += OnNsfwModeChanged;
    }
    
    // Choose and load image based on nsfw setting
    private BitmapImage GetImage()
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var version = CivitModel.ModelVersions?.FirstOrDefault();
        var images = version?.Images;

        var image = images?.FirstOrDefault(image => nsfwEnabled || image.Nsfw == "None");
        if (image != null)
        {
            return new BitmapImage(new Uri(image.Url));
        }
        // Otherwise Default image
        return new BitmapImage(
            new Uri("pack://application:,,,/StabilityMatrix;component/Assets/noimage.png"));
    }

    // On any mode changes, update the image
    private void OnNsfwModeChanged(object? sender, bool value)
    {
        CardImage = GetImage();
    }

    [RelayCommand]
    private void OpenModel()
    {
        ProcessRunner.OpenUrl($"https://civitai.com/models/{CivitModel.Id}");
    }

    [RelayCommand]
    private async Task Import(CivitModel model)
    {
        Text = "Downloading...";
        
        // Get latest version
        if (!(model.ModelVersions?.Count > 0))
        {
            snackbarService.ShowSnackbarAsync(
                "This model has no versions available for download",
                "Model has no versions available", ControlAppearance.Caution).SafeFireAndForget();
            return;
        }
        var modelVersion = model.ModelVersions[0];
        
        // Get latest version file
        if (!(modelVersion.Files?.Count > 0))
        {
            snackbarService.ShowSnackbarAsync(
                "This model has no files available for download",
                "Model has no files available", ControlAppearance.Caution).SafeFireAndForget();
            return;
        }
        var modelFile = modelVersion.Files[0];
        
        var downloadFolder = Path.Combine(settingsManager.ModelsDirectory,
            model.Type.ConvertTo<SharedFolderType>().GetStringValue());
        // Folders might be missing if user didn't install any packages yet
        Directory.CreateDirectory(downloadFolder);
        var downloadPath = Path.GetFullPath(Path.Combine(downloadFolder, modelFile.Name));
        
        // Do the download
        var downloadTask = downloadService.DownloadToFileAsync(modelFile.DownloadUrl, downloadPath, 
            new Progress<ProgressReport>(report =>
            {
                Value = report.Percentage;
                Text = $"Downloading... {report.Percentage}%";
            }));
        var downloadResult = await snackbarService.TryAsync(downloadTask, "Could not download file");
        
        // For exceptions other than ApiException or TaskCanceledException, log error
        if (downloadResult is {Exception: not null and not 
                (HttpRequestException or ApiException or TaskCanceledException)})
        {
            Logger.Error(downloadResult.Exception, "Unexpected error during model download");
            return;
        }
        
        // When sha256 is available, validate the downloaded file
        var fileExpectedSha256 = modelFile.Hashes.SHA256;
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
                DelayedClearProgress(TimeSpan.FromMilliseconds(800));
                snackbarService.ShowSnackbarAsync(
                    "This may be caused by network or server issues from CivitAI, please try again in a few minutes.",
                    "Download failed hash validation").SafeFireAndForget();
                return;
            }
            snackbarService.ShowSnackbarAsync($"{model.Type} {model.Name} imported successfully!",
                "Import complete", ControlAppearance.Info).SafeFireAndForget();
        }
        
        IsIndeterminate = true;
        
        // Save connected model info
        var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Name);
        var modelInfo = new ConnectedModelInfo(CivitModel, modelVersion, modelFile, DateTime.UtcNow);
        await modelInfo.SaveJsonToDirectory(downloadFolder, modelFileName);
        
        // If available, save a model image
        if (modelVersion.Images != null && modelVersion.Images.Any())
        {
            var image = modelVersion.Images[0];
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
