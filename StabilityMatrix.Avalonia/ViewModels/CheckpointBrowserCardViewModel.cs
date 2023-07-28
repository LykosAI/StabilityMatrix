using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using Octokit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Notification = Avalonia.Controls.Notifications.Notification;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class CheckpointBrowserCardViewModel : ProgressViewModel

{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    public CivitModel CivitModel { get; init; }
    public override bool IsTextVisible => Value > 0;
    
    [ObservableProperty] private Uri? cardImage;
    [ObservableProperty] private bool isImporting;
    [ObservableProperty] private string updateCardText = string.Empty;
    [ObservableProperty] private bool showUpdateCard;

    public CheckpointBrowserCardViewModel(
        CivitModel civitModel,
        IDownloadService downloadService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService)
    {
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        CivitModel = civitModel;

        UpdateImage();

        CheckIfInstalled();

        // Update image when nsfw setting changes
        settingsManager.RegisterPropertyChangedHandler(
            s => s.ModelBrowserNsfwEnabled,
            _ => Dispatcher.UIThread.Post(UpdateImage));
    }

    private void CheckIfInstalled()
    {
        if (Design.IsDesignMode)
        {
            UpdateCardText = "Installed";
            ShowUpdateCard = true;
            return;
        }
        
        if (CivitModel.ModelVersions == null) return;
        
        var installedModels = settingsManager.Settings.InstalledModelHashes;
        if (!installedModels.Any()) return;
        
        // check if latest version is installed
        var latestVersion = CivitModel.ModelVersions.FirstOrDefault();
        if (latestVersion == null) return;
        
        var latestVersionInstalled = latestVersion.Files != null && latestVersion.Files.Any(file =>
            file is {Type: CivitFileType.Model, Hashes.BLAKE3: not null} &&
            installedModels.Contains(file.Hashes.BLAKE3));

        // check if any of the ModelVersion.Files.Hashes.BLAKE3 hashes are in the installedModels list
        var anyVersionInstalled = latestVersionInstalled || CivitModel.ModelVersions.Any(version =>
            version.Files != null && version.Files.Any(file =>
                file is {Type: CivitFileType.Model, Hashes.BLAKE3: not null} &&
                installedModels.Contains(file.Hashes.BLAKE3)));

        UpdateCardText = latestVersionInstalled ? "Installed" :
            anyVersionInstalled ? "Update Available" : string.Empty;

        ShowUpdateCard = anyVersionInstalled;
    }

    // Choose and load image based on nsfw setting
    private void UpdateImage()
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var version = CivitModel.ModelVersions?.FirstOrDefault();
        var images = version?.Images;

        // Try to find a valid image
        var image = images?.FirstOrDefault(image => nsfwEnabled || image.Nsfw == "None");
        if (image != null)
        {
            // var imageStream = await downloadService.GetImageStreamFromUrl(image.Url);
            // Dispatcher.UIThread.Post(() => { CardImage = new Bitmap(imageStream); });
            CardImage = new Uri(image.Url);
            return;
        }
        
        // If no valid image found, use no image
        CardImage = Assets.NoImage;

        // var assetStream = AssetLoader.Open(new Uri("avares://StabilityMatrix.Avalonia/Assets/noimage.png"));
        // Otherwise Default image
        // Dispatcher.UIThread.Post(() => { CardImage = new Bitmap(assetStream); });
    }

    [RelayCommand]
    private void OpenModel()
    {
        ProcessRunner.OpenUrl($"https://civitai.com/models/{CivitModel.Id}");
    }

    [RelayCommand]
    private async Task Import(CivitModel model)
    {
        await DoImport(model);
        CheckIfInstalled();
    }

    [RelayCommand]
    private async Task ShowVersionDialog(CivitModel model)
    {
        var versions = model.ModelVersions;
        if (versions is null || versions.Count == 0)
        {
            notificationService.Show(new Notification("Model has no versions available",
                "This model has no versions available for download", NotificationType.Warning));
            return;
        }
        
        var dialog = new BetterContentDialog
        {
            Title = model.Name,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            MaxDialogWidth = 750,
        };
        
        var viewModel = dialogFactory.Get<SelectModelVersionViewModel>();
        viewModel.Dialog = dialog;
        viewModel.Versions = versions.Select(version =>
                new ModelVersionViewModel(
                    settingsManager.Settings.InstalledModelHashes ?? new HashSet<string>(), version))
            .ToImmutableArray();
        viewModel.SelectedVersionViewModel = viewModel.Versions[0];
        
        dialog.Content = new SelectModelVersionDialog
        {
            DataContext = viewModel
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var selectedVersion = viewModel?.SelectedVersionViewModel?.ModelVersion;
        var selectedFile = viewModel?.SelectedFile?.CivitFile;

        await Task.Delay(100);
        await DoImport(model, selectedVersion, selectedFile);
    }

    private async Task DoImport(CivitModel model, CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null)
    {
        IsImporting = true;
        Text = "Downloading...";

        // Holds files to be deleted on errors
        var filesForCleanup = new HashSet<FilePath>();

        // Set Text when exiting, finally block will set 100 and delay clear progress
        try
        {
            // Get latest version
            var modelVersion = selectedVersion ?? model.ModelVersions?.FirstOrDefault();
            if (modelVersion is null)
            {
                notificationService.Show(new Notification("Model has no versions available",
                    "This model has no versions available for download", NotificationType.Warning));
                Text = "Unable to Download";
                return;
            }

            // Get latest version file
            var modelFile = selectedFile ??
                            modelVersion.Files?.FirstOrDefault(x => x.Type == CivitFileType.Model);
            if (modelFile is null)
            {
                notificationService.Show(new Notification("Model has no files available",
                    "This model has no files available for download", NotificationType.Warning));
                Text = "Unable to Download";
                return;
            }

            var downloadFolder = Path.Combine(settingsManager.ModelsDirectory,
                model.Type.ConvertTo<SharedFolderType>().GetStringValue());
            // Folders might be missing if user didn't install any packages yet
            Directory.CreateDirectory(downloadFolder);
            var downloadPath = Path.GetFullPath(Path.Combine(downloadFolder, modelFile.Name));
            filesForCleanup.Add(downloadPath);

            // Do the download
            var progressId = Guid.NewGuid();
            var downloadTask = downloadService.DownloadToFileAsync(modelFile.DownloadUrl,
                downloadPath,
                new Progress<ProgressReport>(report =>
                {
                    if (Math.Abs(report.Percentage - Value) > 0.1)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Value = report.Percentage;
                            Text = $"Downloading... {report.Percentage}%";
                        });
                        EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                            modelFile.Name, report));
                    }
                }));

            var downloadResult =
                await notificationService.TryAsync(downloadTask, "Could not download file");

            // Failed download handling
            if (downloadResult.Exception is not null)
            {
                // For exceptions other than ApiException or TaskCanceledException, log error
                var logLevel = downloadResult.Exception switch
                {
                    HttpRequestException or ApiException or TaskCanceledException => LogLevel.Warn,
                    _ => LogLevel.Error
                };
                Logger.Log(logLevel, downloadResult.Exception, "Error during model download");

                Text = "Download Failed";
                EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                    modelFile.Name, new ProgressReport(0f), true));
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
                    EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                        modelFile.Name, progress));
                });
                var sha256 = await FileHash.GetSha256Async(downloadPath, hashProgress);
                if (sha256 != fileExpectedSha256.ToLowerInvariant())
                {
                    Text = "Import Failed!";
                    DelayedClearProgress(TimeSpan.FromMilliseconds(800));
                    notificationService.Show(new Notification("Download failed hash validation",
                        "This may be caused by network or server issues from CivitAI, please try again in a few minutes.",
                        NotificationType.Error));
                    Text = "Download Failed";
                    
                    EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                        modelFile.Name, new ProgressReport(0f), true));
                    return;
                }

                settingsManager.Transaction(
                    s => s.InstalledModelHashes.Add(modelFile.Hashes.BLAKE3));

                notificationService.Show(new Notification("Import complete",
                    $"{model.Type} {model.Name} imported successfully!", NotificationType.Success));
            }

            IsIndeterminate = true;

            // Save connected model info
            var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Name);
            var modelInfo =
                new ConnectedModelInfo(CivitModel, modelVersion, modelFile, DateTime.UtcNow);
            var modelInfoPath = Path.GetFullPath(Path.Combine(
                downloadFolder, modelFileName + ConnectedModelInfo.FileExtension));
            filesForCleanup.Add(modelInfoPath);
            await modelInfo.SaveJsonToDirectory(downloadFolder, modelFileName);

            // If available, save a model image
            if (modelVersion.Images != null && modelVersion.Images.Any())
            {
                var image = modelVersion.Images[0];
                var imageExtension = Path.GetExtension(image.Url).TrimStart('.');
                if (imageExtension is "jpg" or "jpeg" or "png")
                {
                    var imageDownloadPath = Path.GetFullPath(Path.Combine(downloadFolder,
                        $"{modelFileName}.preview.{imageExtension}"));
                    filesForCleanup.Add(imageDownloadPath);
                    var imageTask =
                        downloadService.DownloadToFileAsync(image.Url, imageDownloadPath);
                    await notificationService.TryAsync(imageTask, "Could not download preview image");
                }
            }

            // Successful - clear cleanup list
            filesForCleanup.Clear();
            
            EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                modelFile.Name, new ProgressReport(1f, "Import complete")));

            Text = "Import complete!";
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally
        {
            foreach (var file in filesForCleanup.Where(file => file.Exists))
            {
                file.Delete();
                Logger.Info($"Download cleanup: Deleted file {file}");
            }

            IsIndeterminate = false;
            Value = 100;
            CheckIfInstalled();
            DelayedClearProgress(TimeSpan.FromMilliseconds(800));
        }
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay).ContinueWith(_ =>
        {
            Text = string.Empty;
            Value = 0;
            IsImporting = false;
        });
    }
}
