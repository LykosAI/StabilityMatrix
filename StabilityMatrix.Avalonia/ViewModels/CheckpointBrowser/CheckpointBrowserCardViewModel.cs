﻿using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[ManagedService]
[RegisterTransient<CheckpointBrowserCardViewModel>]
public partial class CheckpointBrowserCardViewModel : ProgressViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IDownloadService downloadService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    private readonly IModelIndexService modelIndexService;
    private readonly IModelImportService modelImportService;
    private readonly ILiteDbContext liteDbContext;
    private readonly CivitCompatApiManager civitApi;
    private readonly INavigationService<MainWindowViewModel> navigationService;

    public Action<CheckpointBrowserCardViewModel>? OnDownloadStart { get; set; }

    public CivitModel CivitModel
    {
        get => civitModel;
        set
        {
            civitModel = value;
            IsFavorite = settingsManager.Settings.FavoriteModels.Contains(value.Id);
            UpdateImage();
            CheckIfInstalled();
        }
    }
    private CivitModel civitModel;

    public int Order { get; set; }

    public override bool IsTextVisible => Value > 0;

    [ObservableProperty]
    private Uri? cardImage;

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string updateCardText = string.Empty;

    [ObservableProperty]
    private bool showUpdateCard;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private bool showSantaHats = true;

    public CheckpointBrowserCardViewModel(
        IDownloadService downloadService,
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService,
        IModelIndexService modelIndexService,
        IModelImportService modelImportService,
        ILiteDbContext liteDbContext,
        CivitCompatApiManager civitApi,
        INavigationService<MainWindowViewModel> navigationService
    )
    {
        this.downloadService = downloadService;
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        this.modelIndexService = modelIndexService;
        this.modelImportService = modelImportService;
        this.liteDbContext = liteDbContext;
        this.civitApi = civitApi;
        this.navigationService = navigationService;

        // Update image when nsfw setting changes
        AddDisposable(
            settingsManager.RegisterPropertyChangedHandler(
                s => s.ModelBrowserNsfwEnabled,
                _ => Dispatcher.UIThread.Post(UpdateImage)
            ),
            settingsManager.RegisterPropertyChangedHandler(
                s => s.HideEarlyAccessModels,
                _ => Dispatcher.UIThread.Post(UpdateImage)
            )
        );

        ShowSantaHats = settingsManager.Settings.IsHolidayModeActive;
    }

    private void CheckIfInstalled()
    {
        if (Design.IsDesignMode)
        {
            UpdateCardText = "Installed";
            ShowUpdateCard = true;
            return;
        }

        if (CivitModel.ModelVersions == null)
            return;

        var installedModels = modelIndexService.ModelIndexBlake3Hashes;
        if (installedModels.Count == 0)
            return;

        // check if latest version is installed
        var latestVersion = CivitModel.ModelVersions.FirstOrDefault();
        if (latestVersion == null)
            return;

        var latestVersionInstalled =
            latestVersion.Files != null
            && latestVersion.Files.Any(file =>
                file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                && installedModels.Contains(file.Hashes.BLAKE3)
            );

        // check if any of the ModelVersion.Files.Hashes.BLAKE3 hashes are in the installedModels list
        var anyVersionInstalled =
            latestVersionInstalled
            || CivitModel.ModelVersions.Any(version =>
                version.Files != null
                && version.Files.Any(file =>
                    file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                    && installedModels.Contains(file.Hashes.BLAKE3)
                )
            );

        UpdateCardText =
            latestVersionInstalled ? "Installed"
            : anyVersionInstalled ? "Update Available"
            : string.Empty;

        ShowUpdateCard = anyVersionInstalled;
    }

    private void UpdateImage()
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var hideEarlyAccessModels = settingsManager.Settings.HideEarlyAccessModels;
        var version = CivitModel.ModelVersions?.FirstOrDefault(v =>
            !hideEarlyAccessModels || !v.IsEarlyAccess
        );
        var images = version?.Images;

        // Try to find a valid image
        var image = images
            ?.Where(img =>
                LocalModelFile.SupportedImageExtensions.Any(img.Url.Contains) && img.Type == "image"
            )
            .FirstOrDefault(image => nsfwEnabled || image.NsfwLevel <= 1);
        if (image != null)
        {
            CardImage = new Uri(image.Url);
            return;
        }

        // If no valid image found, use no image
        CardImage = Assets.NoImage;
    }

    [RelayCommand]
    private void OpenModel()
    {
        ProcessRunner.OpenUrl($"https://civitai.com/models/{CivitModel.Id}");
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (settingsManager.Settings.FavoriteModels.Contains(CivitModel.Id))
        {
            settingsManager.Transaction(s => s.FavoriteModels.Remove(CivitModel.Id));
        }
        else
        {
            settingsManager.Transaction(s => s.FavoriteModels.Add(CivitModel.Id));
        }

        IsFavorite = settingsManager.Settings.FavoriteModels.Contains(CivitModel.Id);
    }

    [RelayCommand]
    public void SearchAuthor()
    {
        EventManager.Instance.OnNavigateAndFindCivitAuthorRequested(CivitModel.Creator?.Username);
    }

    private async Task DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null
    )
    {
        IsImporting = true;
        IsLoading = true;
        Text = "Downloading...";

        OnDownloadStart?.Invoke(this);

        // Get latest version
        var modelVersion = selectedVersion ?? model.ModelVersions?.FirstOrDefault();
        if (modelVersion is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            Text = "Unable to Download";
            return;
        }

        // Get latest version file
        var modelFile =
            selectedFile ?? modelVersion.Files?.FirstOrDefault(x => x.Type == CivitFileType.Model);
        if (modelFile is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no files available",
                    "This model has no files available for download",
                    NotificationType.Warning
                )
            );
            Text = "Unable to Download";
            return;
        }

        await modelImportService.DoImport(
            model,
            downloadFolder,
            modelVersion,
            modelFile,
            onImportComplete: () =>
            {
                Text = "Import Complete";

                IsIndeterminate = false;
                Value = 100;
                CheckIfInstalled();
                DelayedClearProgress(TimeSpan.FromMilliseconds(800));

                return Task.CompletedTask;
            },
            onImportCanceled: () =>
            {
                Text = "Cancelled";
                DelayedClearProgress(TimeSpan.FromMilliseconds(500));

                return Task.CompletedTask;
            },
            onImportFailed: () =>
            {
                Text = "Download Failed";
                DelayedClearProgress(TimeSpan.FromMilliseconds(800));

                return Task.CompletedTask;
            }
        );
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                Text = string.Empty;
                Value = 0;
                IsImporting = false;
                IsLoading = false;
            });
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlRegex();
}
