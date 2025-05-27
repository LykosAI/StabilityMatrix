using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivitDetailsPage))]
[ManagedService]
[RegisterTransient<CivitDetailsPageViewModel>]
public partial class CivitDetailsPageViewModel(
    ISettingsManager settingsManager,
    CivitCompatApiManager civitApi,
    ILogger<CivitDetailsPageViewModel> logger,
    INotificationService notificationService,
    INavigationService<MainWindowViewModel> navigationService,
    IModelIndexService modelIndexService
) : DisposableViewModelBase
{
    public required CivitModel CivitModel { get; set; }

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
    [NotifyPropertyChangedFor(nameof(LastUpdated))]
    public partial ModelVersionViewModel? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowNsfw { get; set; }

    [ObservableProperty]
    public partial bool HideEarlyAccess { get; set; }

    [ObservableProperty]
    public partial bool ShowTrainingData { get; set; }

    [ObservableProperty]
    public partial string SelectedInstallLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableInstallLocations { get; set; } = [];

    public string LastUpdated =>
        SelectedVersion?.ModelVersion.PublishedAt?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty;

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

        var earlyAccessPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(HideEarlyAccess))
            .Select(_ => (Func<CivitModelVersion, bool>)ShouldIncludeVersion)
            .StartWith(ShouldIncludeVersion)
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        AddDisposable(
            modelVersionCache
                .Connect()
                .DeferUntilLoaded()
                .Filter(earlyAccessPredicate)
                .Transform(modelVersion => new ModelVersionViewModel(modelIndexService, modelVersion))
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
                .Transform(file => new CivitFileViewModel(modelIndexService, file)
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

    private bool ShouldIncludeVersion(CivitModelVersion? version)
    {
        if (Design.IsDesignMode)
            return true;

        if (version == null)
            return false;

        return !version.IsEarlyAccess || !HideEarlyAccess;
    }

    private ObservableCollection<string> LoadInstallLocations(CivitFile selectedFile)
    {
        if (Design.IsDesignMode)
            return ["Models/StableDiffusion", "Custom..."];

        var installLocations = new ObservableCollection<string>();

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
        return installLocations;
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
            )
        )
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.DiffusionModels.GetStringValue());
        }

        if (
            modelType is CivitModelType.Checkpoint
            && baseModelType == CivitBaseModelType.WanVideo.GetStringValue()
        )
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.DiffusionModels.GetStringValue());
        }

        return rootModelsDirectory.JoinDir(modelType.ConvertTo<SharedFolderType>().GetStringValue());
    }
}
