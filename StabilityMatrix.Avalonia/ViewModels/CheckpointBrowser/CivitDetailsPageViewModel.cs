using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivitDetailsPage))]
[ManagedService]
[RegisterTransient<CivitDetailsPageViewModel>]
public partial class CivitDetailsPageViewModel(
    ISettingsManager settingsManager,
    CivitCompatApiManager civitApi,
    ICivitTRPCApi civitTrpcApi,
    ILogger<CivitDetailsPageViewModel> logger,
    INotificationService notificationService,
    INavigationService<MainWindowViewModel> navigationService,
    IModelIndexService modelIndexService,
    IServiceManager<ViewModelBase> vmFactory,
    IModelImportService modelImportService
) : DisposableViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInferenceDefaultsSection))]
    public required partial CivitModel CivitModel { get; set; }

    private List<string> ignoredFileNameFormatVars =
    [
        "seed",
        "prompt",
        "negative_prompt",
        "model_hash",
        "sampler",
        "cfgscale",
        "steps",
        "width",
        "height",
        "project_type",
        "project_name",
    ];

    public IEnumerable<FileNameFormatVar> ModelFileNameFormatVars =>
        FileNameFormatProvider
            .GetSampleForModelBrowser()
            .Substitutions.Where(kv => !ignoredFileNameFormatVars.Contains(kv.Key))
            .Select(kv => new FileNameFormatVar { Variable = $"{{{kv.Key}}}", Example = kv.Value.Invoke() });

    private SourceCache<CivitImage, string> imageCache = new(x => x.Url);

    public IObservableCollection<ImageSource> ImageSources { get; set; } =
        new ObservableCollectionExtended<ImageSource>();

    private SourceCache<CivitModelVersion, int> modelVersionCache = new(x => x.Id);

    public IObservableCollection<ModelVersionViewModel> ModelVersions { get; set; } =
        new ObservableCollectionExtended<ModelVersionViewModel>();

    private SourceCache<CivitFile, int> civitFileCache = new(x => x.Id);

    public IObservableCollection<CivitFileViewModel> CivitFiles { get; set; } =
        new ObservableCollectionExtended<CivitFileViewModel>();

    [ObservableProperty]
    public partial ObservableCollection<CivitFileViewModel> SelectedFiles { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(LastUpdated),
        nameof(ShortSha256),
        nameof(BaseModelType),
        nameof(ModelFileNameFormat)
    )]
    public partial ModelVersionViewModel? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionRowSpan))]
    public partial string ModelVersionDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowNsfw { get; set; }

    [ObservableProperty]
    public partial bool HideEarlyAccess { get; set; }

    [ObservableProperty]
    public partial bool ShowTrainingData { get; set; }

    [ObservableProperty]
    public partial bool HideInstalledModels { get; set; }

    [ObservableProperty]
    public partial string SelectedInstallLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableInstallLocations { get; set; } = [];

    [ObservableProperty]
    [CustomValidation(typeof(CivitDetailsPageViewModel), nameof(ValidateModelFileNameFormat))]
    public partial string? ModelFileNameFormat { get; set; }

    [ObservableProperty]
    public partial string? ModelNameFormatSample { get; set; }

    [ObservableProperty]
    public partial SamplerCardViewModel SamplerCardViewModel { get; set; } =
        vmFactory.Get<SamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDimensionsEnabled = true;
            samplerCard.IsCfgScaleEnabled = true;
            samplerCard.IsSamplerSelectionEnabled = true;
            samplerCard.IsSchedulerSelectionEnabled = true;
            samplerCard.DenoiseStrength = 1.0d;
            samplerCard.EnableAddons = false;
            samplerCard.IsDenoiseStrengthEnabled = false;
        });

    [ObservableProperty]
    public partial bool IsInferenceDefaultsEnabled { get; set; } = false;

    public string LastUpdated =>
        SelectedVersion?.ModelVersion.PublishedAt?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty;

    public string ShortSha256 =>
        SelectedVersion?.ModelVersion.Files?.FirstOrDefault()?.Hashes.ShortSha256 ?? string.Empty;

    public string BaseModelType => SelectedVersion?.ModelVersion.BaseModel?.Trim() ?? string.Empty;

    public string CivitUrl => $@"https://civitai.com/models/{CivitModel.Id}";

    public int DescriptionRowSpan => string.IsNullOrWhiteSpace(ModelVersionDescription) ? 2 : 1;

    public bool ShowInferenceDefaultsSection => CivitModel.Type == CivitModelType.Checkpoint;

    protected override async Task OnInitialLoadedAsync()
    {
        if (
            !Design.IsDesignMode
            && (CivitModel.ModelVersions?.Select(x => x.Files).Any(x => x == null || x.Count == 0) ?? true)
        )
        {
            try
            {
                CivitModel = await civitApi.GetModelById(CivitModel.Id);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to load CivitModel {Id}", CivitModel.Id);
                notificationService.Show(
                    Resources.Label_UnexpectedErrorOccurred,
                    e.Message,
                    NotificationType.Error
                );
                return;
            }
        }

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ShowNsfw,
                settings => settings.ModelBrowserNsfwEnabled,
                true
            )
        );
        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.HideEarlyAccess,
                settings => settings.HideEarlyAccessModels,
                true
            )
        );
        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ShowTrainingData,
                settings => settings.ShowTrainingDataInModelBrowser,
                true
            )
        );
        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.HideInstalledModels,
                settings => settings.HideInstalledModelsInModelBrowser,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ModelFileNameFormat,
                settings => settings.CivitModelBrowserFileNamePattern,
                true
            )
        );

        AddDisposable(
            this.WhenPropertyChanged(vm => vm.ModelFileNameFormat)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(formatProperty =>
                {
                    var provider = new FileNameFormatProvider
                    {
                        CivitModel = CivitModel,
                        CivitModelVersion = SelectedVersion?.ModelVersion,
                        CivitFile = SelectedVersion?.ModelVersion.Files?.FirstOrDefault(),
                    };

                    var template = formatProperty.Value ?? string.Empty;

                    if (
                        !string.IsNullOrEmpty(template)
                        && provider.Validate(template) == ValidationResult.Success
                    )
                    {
                        var format = FileNameFormat.Parse(template, provider);
                        ModelNameFormatSample = "Example: " + format.GetFileName() + ".safetensors";
                    }
                    else
                    {
                        // Use default format if empty
                        var defaultFormat = FileNameFormat.Parse(
                            FileNameFormat.DefaultModelBrowserTemplate,
                            provider
                        );
                        ModelNameFormatSample = "Example: " + defaultFormat.GetFileName() + ".safetensors";
                    }
                })
        );

        var earlyAccessPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(HideEarlyAccess) or nameof(HideInstalledModels))
            .Select(_ => (Func<ModelVersionViewModel, bool>)ShouldIncludeVersion)
            .StartWith(ShouldIncludeVersion)
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        AddDisposable(
            modelVersionCache
                .Connect()
                .DeferUntilLoaded()
                .Transform(modelVersion => new ModelVersionViewModel(modelIndexService, modelVersion))
                .DisposeMany()
                .Filter(earlyAccessPredicate)
                .SortAndBind(
                    ModelVersions,
                    SortExpressionComparer<ModelVersionViewModel>.Descending(v => v.ModelVersion.PublishedAt)
                )
                .ObserveOn(SynchronizationContext.Current!)
                .DisposeMany()
                .Subscribe()
        );

        var showNsfwPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(ShowNsfw))
            .Select(_ => (Func<CivitImage, bool>)ShouldShowNsfw)
            .StartWith(ShouldShowNsfw)
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        AddDisposable(
            imageCache
                .Connect()
                .Filter(showNsfwPredicate)
                .Transform(x => new ImageSource(new Uri(x.Url)))
                .Bind(ImageSources)
                .ObserveOn(SynchronizationContext.Current!)
                .DisposeMany()
                .Subscribe()
        );

        var includeTrainingDataPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(ShowTrainingData))
            .Select(_ => (Func<CivitFile, bool>)(ShouldIncludeCivitFile))
            .StartWith(ShouldIncludeCivitFile)
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        AddDisposable(
            civitFileCache
                .Connect()
                .Filter(includeTrainingDataPredicate)
                .Transform(file => new CivitFileViewModel(
                    modelIndexService,
                    settingsManager,
                    file,
                    vmFactory,
                    DownloadModelAsync
                )
                {
                    InstallLocations = new ObservableCollection<string>(LoadInstallLocations(file)),
                })
                .Bind(CivitFiles)
                .ObserveOn(SynchronizationContext.Current!)
                .DisposeMany()
                .Subscribe()
        );

        modelVersionCache.EditDiff(CivitModel.ModelVersions ?? [], (a, b) => a.Id == b.Id);

        SelectedVersion = ModelVersions.FirstOrDefault();
        Description = $"""<html><body class="markdown-body">{CivitModel.Description}</body></html>""";
    }

    [RelayCommand]
    private void GoBack() => navigationService.GoBack();

    public async Task DownloadModelAsync(CivitFileViewModel viewModel, string? locationKey = null)
    {
        DirectoryPath? finalDestinationDir = null;
        var effectiveLocationKeyForPreference = string.Empty;

        switch (locationKey)
        {
            case null:
            {
                var preferenceUsed = false;
                if (
                    settingsManager.Settings.ModelTypeDownloadPreferences.TryGetValue(
                        CivitModel.Type.ToString(),
                        out var preference
                    )
                )
                {
                    if (
                        preference.SelectedInstallLocation == "Custom..."
                        && !string.IsNullOrWhiteSpace(preference.CustomInstallLocation)
                    )
                    {
                        finalDestinationDir = new DirectoryPath(preference.CustomInstallLocation);
                        effectiveLocationKeyForPreference = "Custom...";
                        preferenceUsed = true;
                    }
                    else if (
                        !string.IsNullOrWhiteSpace(preference.SelectedInstallLocation)
                        && viewModel.InstallLocations.Contains(preference.SelectedInstallLocation)
                    )
                    {
                        var basePath = new DirectoryPath(settingsManager.ModelsDirectory);
                        finalDestinationDir = new DirectoryPath(
                            Path.Combine(
                                basePath.ToString(),
                                preference
                                    .SelectedInstallLocation.Replace("Models\\", "")
                                    .Replace("Models/", "")
                            )
                        );
                        effectiveLocationKeyForPreference = preference.SelectedInstallLocation;
                        preferenceUsed = true;
                    }
                }

                if (!preferenceUsed)
                {
                    finalDestinationDir = GetSharedFolderPath(
                        settingsManager.ModelsDirectory,
                        viewModel.CivitFile.Type,
                        CivitModel.Type,
                        CivitModel.BaseModelType
                    );
                    effectiveLocationKeyForPreference =
                        viewModel.InstallLocations.FirstOrDefault(loc =>
                            loc != "Custom..."
                            && finalDestinationDir
                                .ToString()
                                .Contains(loc.Replace("Models\\", "").Replace("Models/", ""))
                        ) ?? viewModel.InstallLocations.First();
                }

                break;
            }
            case "Custom...":
            {
                effectiveLocationKeyForPreference = "Custom...";
                var files = await App.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title = "Select Download Folder",
                        AllowMultiple = false,
                        SuggestedStartLocation = await App.StorageProvider.TryGetFolderFromPathAsync(
                            Path.Combine(
                                settingsManager.ModelsDirectory,
                                CivitModel.Type.ConvertTo<SharedFolderType>().GetStringValue()
                            )
                        ),
                    }
                );

                if (files.FirstOrDefault()?.TryGetLocalPath() is { } customPath)
                {
                    finalDestinationDir = new DirectoryPath(customPath);
                }
                else
                {
                    return;
                }

                break;
            }
            default:
            {
                effectiveLocationKeyForPreference = locationKey;
                var basePath = new DirectoryPath(settingsManager.ModelsDirectory);
                finalDestinationDir = new DirectoryPath(
                    Path.Combine(
                        basePath.ToString(),
                        locationKey.Replace("Models\\", "").Replace("Models/", "")
                    )
                );
                break;
            }
        }

        if (finalDestinationDir is null)
        {
            notificationService.Show(
                Resources.Label_UnexpectedErrorOccurred,
                "Could not determine final destination directory.",
                NotificationType.Error
            );
            return;
        }

        var fileNameOverride = ParseFileNameFormat(
            CivitModel,
            SelectedVersion?.ModelVersion,
            viewModel.CivitFile
        );

        await modelImportService.DoImport(
            CivitModel,
            finalDestinationDir,
            SelectedVersion?.ModelVersion,
            viewModel.CivitFile,
            fileNameOverride,
            inferenceDefaults: IsInferenceDefaultsEnabled ? SamplerCardViewModel : null
        );

        notificationService.Show(
            Resources.Label_DownloadStarted,
            string.Format(
                Resources.Label_DownloadWillBeSavedToLocation,
                viewModel.CivitFile.Name,
                finalDestinationDir.JoinFile(fileNameOverride).Directory
            )
        );

        if (CivitModel.Type != CivitModelType.Unknown)
        {
            var modelTypeKey = CivitModel.Type.ToString();
            var newPreference = new LastDownloadLocationInfo
            {
                SelectedInstallLocation = effectiveLocationKeyForPreference,
                CustomInstallLocation =
                    (effectiveLocationKeyForPreference == "Custom...")
                        ? finalDestinationDir.ToString()
                        : null,
            };
            settingsManager.Transaction(s =>
            {
                s.ModelTypeDownloadPreferences[modelTypeKey] = newPreference;
            });
        }
    }

    [RelayCommand]
    private async Task ShowBulkDownloadDialogAsync()
    {
        var dialogVm = vmFactory.Get<ConfirmBulkDownloadDialogViewModel>(vm => vm.Model = CivitModel);
        var dialog = dialogVm.GetDialog();
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        foreach (var file in dialogVm.FilesToDownload)
        {
            var sharedFolderPath = GetSharedFolderPath(
                new DirectoryPath(settingsManager.ModelsDirectory),
                file.FileViewModel.CivitFile.Type,
                CivitModel.Type,
                CivitModel.BaseModelType
            );

            var folderName = Path.GetInvalidFileNameChars()
                .Aggregate(CivitModel.Name, (current, c) => current.Replace(c, '_'));

            var destinationDir = new DirectoryPath(sharedFolderPath, folderName);
            destinationDir.Create();

            var fileNameOverride = ParseFileNameFormat(
                CivitModel,
                file.ModelVersion,
                file.FileViewModel.CivitFile
            );

            await modelImportService.DoImport(
                CivitModel,
                destinationDir,
                file.ModelVersion,
                file.FileViewModel.CivitFile,
                fileNameOverride
            );
        }

        notificationService.Show(
            Resources.Label_BulkDownloadStarted,
            string.Format(Resources.Label_BulkDownloadStartedMessage, dialogVm.FilesToDownload.Count),
            NotificationType.Success
        );
    }

    [RelayCommand]
    private async Task ShowImageDialog(ImageSource? image)
    {
        if (image is null)
            return;

        var currentIndex = ImageSources.IndexOf(image);

        // Preload
        await image.GetBitmapAsync();

        var vm = vmFactory.Get<ImageViewerViewModel>();
        vm.ImageSource = image;

        var url = image.RemoteUrl;
        if (url is null)
            return;

        try
        {
            var imageId = Path.GetFileNameWithoutExtension(url.Segments.Last());
            var imageData = await civitTrpcApi.GetImageGenerationData($$$"""{"json":{"id":{{{imageId}}}}}""");
            vm.CivitImageMetadata = imageData.Result.Data.Json;
            vm.CivitImageMetadata.OtherMetadata = GetOtherMetadata(vm.CivitImageMetadata);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load CivitImageMetadata for {Url}", url);
        }

        using var onNext = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(ctx =>
            {
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < ImageSources.Count)
                        {
                            var newImageSource = ImageSources[newIndex];

                            // Preload
                            await newImageSource.GetBitmapAsync();
                            await newImageSource.GetOrRefreshTemplateKeyAsync();
                            sender.ImageSource = newImageSource;

                            try
                            {
                                sender.CivitImageMetadata = null;
                                if (newImageSource.RemoteUrl is not { } newUrl)
                                    return;
                                var imageId = Path.GetFileNameWithoutExtension(newUrl.Segments.Last());
                                var imageData = await civitTrpcApi.GetImageGenerationData(
                                    $$$"""{"json":{"id":{{{imageId}}}}}"""
                                );
                                imageData.Result.Data.Json.OtherMetadata = GetOtherMetadata(
                                    imageData.Result.Data.Json
                                );
                                sender.CivitImageMetadata = imageData.Result.Data.Json;
                            }
                            catch (Exception e)
                            {
                                logger.LogError(e, "Failed to load CivitImageMetadata for {Url}", url);
                            }

                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        vm.NavigateToModelRequested += VmOnNavigateToModelRequested;

        await vm.GetDialog().ShowAsync();
    }

    [RelayCommand]
    private void SearchByAuthor()
    {
        navigationService.GoBack();
        EventManager.Instance.OnNavigateAndFindCivitAuthorRequested(CivitModel.Creator.Username);
    }

    [RelayCommand]
    private async Task DeleteModelVersion(CivitModelVersion modelVersion)
    {
        if (modelVersion.Files == null)
            return;

        var pathsToDelete = new List<string>();

        foreach (var file in modelVersion.Files)
        {
            if (file is not { Type: CivitFileType.Model, Hashes.BLAKE3: not null })
                continue;

            var matchingModels = (await modelIndexService.FindByHashAsync(file.Hashes.BLAKE3)).ToList();

            if (matchingModels.Count == 0)
            {
                await modelIndexService.RefreshIndex();
                matchingModels = (await modelIndexService.FindByHashAsync(file.Hashes.BLAKE3)).ToList();
            }

            if (matchingModels.Count == 0)
            {
                logger.LogWarning(
                    "No matching models found for file {FileName} with hash {Hash}",
                    file.Name,
                    file.Hashes.BLAKE3
                );
                continue;
            }

            foreach (var localModel in matchingModels)
            {
                var checkpointPath = new FilePath(localModel.GetFullPath(settingsManager.ModelsDirectory));
                if (File.Exists(checkpointPath))
                {
                    pathsToDelete.Add(checkpointPath);
                }

                var previewPath = localModel.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
                if (File.Exists(previewPath))
                {
                    pathsToDelete.Add(previewPath);
                }

                var cmInfoPath = checkpointPath.ToString().Replace(checkpointPath.Extension, ".cm-info.json");
                if (File.Exists(cmInfoPath))
                {
                    pathsToDelete.Add(cmInfoPath);
                }
            }
        }

        var confirmDeleteVm = vmFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = pathsToDelete;

        var dialog = confirmDeleteVm.GetDialog();
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete model files for {ModelName}", modelVersion.Name);
        }
        finally
        {
            await modelIndexService.RefreshIndex();
        }
    }

    private void VmOnNavigateToModelRequested(object? sender, int modelId)
    {
        if (sender is not ImageViewerViewModel vm)
            return;

        var detailsPageVm = vmFactory.Get<CivitDetailsPageViewModel>(x =>
            x.CivitModel = new CivitModel { Id = modelId }
        );
        navigationService.NavigateTo(detailsPageVm, BetterSlideNavigationTransition.PageSlideFromRight);

        vm.NavigateToModelRequested -= VmOnNavigateToModelRequested;
        vm.OnCloseButtonClick();
    }

    private bool ShouldIncludeCivitFile(CivitFile file)
    {
        if (ShowTrainingData)
            return true;

        return file.Type is CivitFileType.Model or CivitFileType.PrunedModel or CivitFileType.VAE;
    }

    partial void OnSelectedVersionChanged(ModelVersionViewModel? value)
    {
        imageCache.EditDiff(value?.ModelVersion.Images ?? [], (a, b) => a.Url == b.Url);
        civitFileCache.EditDiff(value?.ModelVersion.Files ?? [], (a, b) => a.Id == b.Id);
        SelectedFiles = new ObservableCollection<CivitFileViewModel>([CivitFiles.FirstOrDefault()]);

        ModelVersionDescription = string.IsNullOrWhiteSpace(value?.ModelVersion.Description)
            ? string.Empty
            : $"""<html><body class="markdown-body">{value.ModelVersion.Description}</body></html>""";
    }

    public override void OnUnloaded()
    {
        ModelVersions.ForEach(x => x.Dispose());
        CivitFiles.ForEach(x => x.Dispose());
        Dispose(true);
        base.OnUnloaded();
    }

    public static ValidationResult ValidateModelFileNameFormat(string? format, ValidationContext context)
    {
        return FileNameFormatProvider.GetSampleForModelBrowser().Validate(format ?? string.Empty);
    }

    private bool ShouldShowNsfw(CivitImage? image)
    {
        if (Design.IsDesignMode)
            return true;

        if (image == null)
            return false;

        return image.NsfwLevel switch
        {
            null or <= 1 => true,
            _ => ShowNsfw,
        };
    }

    private bool ShouldIncludeVersion(ModelVersionViewModel? versionVm)
    {
        if (Design.IsDesignMode)
            return true;

        if (versionVm == null)
            return false;

        var version = versionVm.ModelVersion;

        if (HideInstalledModels && versionVm.IsInstalled)
            return false;

        return !version.IsEarlyAccess || !HideEarlyAccess;
    }

    private ObservableCollection<string> LoadInstallLocations(CivitFile selectedFile)
    {
        if (Design.IsDesignMode)
            return ["Models/StableDiffusion", "Custom..."];

        var installLocations = new List<string>();

        var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);

        var downloadDirectory = GetSharedFolderPath(
            rootModelsDirectory,
            selectedFile.Type,
            CivitModel.Type,
            CivitModel.BaseModelType
        );

        if (!downloadDirectory.ToString().EndsWith("Unknown"))
        {
            installLocations.Add(
                Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, downloadDirectory))
            );
            foreach (
                var directory in downloadDirectory.EnumerateDirectories(
                    "*",
                    EnumerationOptionConstants.AllDirectories
                )
            )
            {
                installLocations.Add(
                    Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, directory))
                );
            }
        }

        if (downloadDirectory.ToString().EndsWith(SharedFolderType.DiffusionModels.GetStringValue()))
        {
            // also add StableDiffusion in case we have an AIO version
            var stableDiffusionDirectory = rootModelsDirectory.JoinDir(
                SharedFolderType.StableDiffusion.GetStringValue()
            );
            installLocations.Add(
                Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, stableDiffusionDirectory))
            );
        }

        installLocations.Add("Custom...");
        return new ObservableCollection<string>(
            installLocations.OrderBy(s => s.Replace(Path.DirectorySeparatorChar.ToString(), string.Empty))
        );
    }

    private static DirectoryPath GetSharedFolderPath(
        DirectoryPath rootModelsDirectory,
        CivitFileType? fileType,
        CivitModelType modelType,
        string? baseModelType
    )
    {
        if (fileType is CivitFileType.VAE)
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.VAE.GetStringValue());
        }

        if (
            modelType is CivitModelType.Checkpoint
            && (
                baseModelType == CivitBaseModelType.Flux1D.GetStringValue()
                || baseModelType == CivitBaseModelType.Flux1S.GetStringValue()
                || baseModelType == CivitBaseModelType.WanVideo.GetStringValue()
            )
        )
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.DiffusionModels.GetStringValue());
        }

        return rootModelsDirectory.JoinDir(modelType.ConvertTo<SharedFolderType>().GetStringValue());
    }

    private IReadOnlyDictionary<string, string> GetOtherMetadata(CivitImageGenerationDataResponse value)
    {
        var metaDict = new Dictionary<string, string>();
        if (value.Metadata?.CfgScale is not null)
            metaDict["CFG"] = value.Metadata.CfgScale.ToString();

        if (value.Metadata?.Steps is not null)
            metaDict["Steps"] = value.Metadata.Steps.ToString();

        if (value.Metadata?.Sampler is not null)
            metaDict["Sampler"] = value.Metadata.Sampler;

        if (value.Metadata?.Seed is not null)
            metaDict["Seed"] = value.Metadata.Seed.ToString();

        if (value.Metadata?.ScheduleType is not null)
            metaDict["Scheduler"] = value.Metadata.ScheduleType;

        if (value.Metadata?.Scheduler is not null)
            metaDict["Scheduler"] = value.Metadata.Scheduler;

        if (value.Metadata?.Rng is not null)
            metaDict["RNG"] = value.Metadata.Rng;

        return metaDict;
    }

    private string ParseFileNameFormat(
        CivitModel? civitModel,
        CivitModelVersion? modelVersion,
        CivitFile? civitFile
    )
    {
        var formatProvider = new FileNameFormatProvider
        {
            CivitModel = civitModel,
            CivitModelVersion = modelVersion,
            CivitFile = civitFile,
        };

        // Parse to format
        if (
            string.IsNullOrEmpty(ModelFileNameFormat)
            || !FileNameFormat.TryParse(ModelFileNameFormat, formatProvider, out var format)
        )
        {
            // Fallback to default
            logger.LogWarning(
                "Failed to parse format template: {ModelFileNameFormat}, using default",
                ModelFileNameFormat
            );

            format = FileNameFormat.Parse(FileNameFormat.DefaultModelBrowserTemplate, formatProvider);
        }

        return format.GetFileName();
    }
}
