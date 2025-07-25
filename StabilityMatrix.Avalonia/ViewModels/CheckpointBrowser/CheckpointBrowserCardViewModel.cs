using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
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
        EventManager.Instance.OnNavigateAndFindCivitAuthorRequested(CivitModel.Creator.Username);
    }

    [RelayCommand]
    private async Task ShowVersionDialog(CivitModel model)
    {
        var versions = model.ModelVersions;
        if (versions is null || versions.Count == 0)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return;
        }

        var newVm = dialogFactory.Get<CivitDetailsPageViewModel>(vm =>
        {
            vm.CivitModel = model;
            return vm;
        });

        navigationService.NavigateTo(newVm, BetterSlideNavigationTransition.PageSlideFromRight);
        return;

        var dialog = new BetterContentDialog
        {
            Title = model.Name,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            CloseOnClickOutside = true,
            MaxDialogWidth = 750,
            MaxDialogHeight = 1000,
        };

        var htmlDescription = $"""<html><body class="markdown-body">{model.Description}</body></html>""";

        var viewModel = dialogFactory.Get<SelectModelVersionViewModel>();
        viewModel.Dialog = dialog;
        viewModel.Title = model.Name;

        viewModel.Description = htmlDescription;
        viewModel.CivitModel = model;
        viewModel.Versions = versions
            .Where(v => !settingsManager.Settings.HideEarlyAccessModels || !v.IsEarlyAccess)
            .Select(version => new ModelVersionViewModel(modelIndexService, version))
            .ToImmutableArray();
        viewModel.SelectedVersionViewModel = viewModel.Versions.Any() ? viewModel.Versions[0] : null;

        // Update with latest version (including files) if we have no files
        if (model.ModelVersions?.FirstOrDefault()?.Files is not { Count: > 0 })
        {
            Task.Run(async () =>
                {
                    Logger.Debug("No files found for model {ModelId}. Updating versions...", model.Id);

                    var latestModel = await civitApi.GetModelById(model.Id);
                    var latestVersions = latestModel.ModelVersions ?? [];

                    // Update our model
                    civitModel.Description = latestModel.Description;
                    civitModel = latestModel;
                    foreach (var version in latestVersions)
                    {
                        if (version.Files is not { Count: > 0 })
                            continue;

                        var targetVersion = model.ModelVersions?.FirstOrDefault(v => v.Id == version.Id);
                        if (targetVersion is null)
                            continue;

                        targetVersion.Files = version.Files;
                        targetVersion.Description = version.Description;
                        targetVersion.DownloadUrl = version.DownloadUrl;
                    }

                    // Reinitialize
                    Logger.Debug("Updating Versions dialog");
                    Dispatcher.UIThread.Post(() =>
                    {
                        var newHtmlDescription =
                            $"""<html><body class="markdown-body">{model.Description}</body></html>""";

                        viewModel.Dialog = dialog;
                        viewModel.Title = latestModel.Name;

                        viewModel.Description = newHtmlDescription;
                        viewModel.CivitModel = latestModel;
                        viewModel.Versions = (latestModel.ModelVersions ?? [])
                            .Where(v => !settingsManager.Settings.HideEarlyAccessModels || !v.IsEarlyAccess)
                            .Select(version => new ModelVersionViewModel(modelIndexService, version))
                            .ToImmutableArray();
                        viewModel.SelectedVersionViewModel = viewModel.Versions.Any()
                            ? viewModel.Versions[0]
                            : null;
                    });

                    // Save to db
                    var upsertResult = await liteDbContext.UpsertCivitModelAsync(latestModel);
                    Logger.Debug(
                        "Update model {ModelId} with latest version: {Result}",
                        model.Id,
                        upsertResult
                    );
                })
                .SafeFireAndForget(e => Logger.Error(e, "Failed to update model {ModelId}", model.Id));
        }

        dialog.Content = new SelectModelVersionDialog { DataContext = viewModel };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var selectedVersion = viewModel?.SelectedVersionViewModel?.ModelVersion;
        var selectedFile = viewModel?.SelectedFile?.CivitFile;

        DirectoryPath downloadPath;
        if (viewModel?.IsCustomSelected is true)
        {
            downloadPath = viewModel.CustomInstallLocation;
        }
        else
        {
            var sharedFolder = model.Type.ConvertTo<SharedFolderType>().GetStringValue();

            if (
                model.BaseModelType == CivitBaseModelType.Flux1D.GetStringValue()
                || model.BaseModelType == CivitBaseModelType.Flux1S.GetStringValue()
                || model.BaseModelType == CivitBaseModelType.WanVideo.GetStringValue()
                || model.BaseModelType == CivitBaseModelType.HunyuanVideo.GetStringValue()
                || selectedFile?.Metadata.Format is CivitModelFormat.GGUF
            )
            {
                sharedFolder = SharedFolderType.DiffusionModels.GetStringValue();
            }

            var defaultPath = Path.Combine(@"Models", sharedFolder);

            var subFolder = viewModel?.SelectedInstallLocation ?? defaultPath;
            subFolder = subFolder.StripStart(@$"Models{Path.DirectorySeparatorChar}");
            downloadPath = Path.Combine(settingsManager.ModelsDirectory, subFolder);
        }

        await Task.Delay(100);
        await DoImport(model, downloadPath, selectedVersion, selectedFile);

        Text = "Import started. Check the downloads tab for progress.";
        Value = 100;
        DelayedClearProgress(TimeSpan.FromMilliseconds(1000));
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
