using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Core;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivArchive;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivArchiveDetailsPage))]
[ManagedService]
[RegisterTransient<CivArchiveDetailsPageViewModel>]
public partial class CivArchiveDetailsPageViewModel(
    ICivArchiveApiClient civArchiveApiClient,
    INavigationService<MainWindowViewModel> navigationService,
    IServiceManager<ViewModelBase> vmFactory,
    IModelImportService modelImportService,
    ISettingsManager settingsManager,
    INotificationService notificationService
) : ViewModelBase
{
    [ObservableProperty]
    public partial string RelativeUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CivArchiveModelDetails? Model { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CivArchiveVersionReference? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial string ModelDescriptionHtml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionDescriptionHtml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDownloadUrl { get; set; }

    public ObservableCollection<CivArchiveModelImage> Images { get; } = [];
    public ObservableCollection<CivArchiveModelFile> Files { get; } = [];
    public ObservableCollection<CivArchiveVersionMirror> Mirrors { get; } = [];

    private static readonly Dictionary<
        string,
        (SharedFolderType Folder, CivitModelType ModelType)
    > ModelTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Checkpoint"] = (SharedFolderType.StableDiffusion, CivitModelType.Checkpoint),
        ["LORA"] = (SharedFolderType.Lora, CivitModelType.LORA),
        ["DoRA"] = (SharedFolderType.Lora, CivitModelType.DoRA),
        ["LoCon"] = (SharedFolderType.LyCORIS, CivitModelType.LoCon),
        ["TextualInversion"] = (SharedFolderType.Embeddings, CivitModelType.TextualInversion),
        ["Hypernetwork"] = (SharedFolderType.Hypernetwork, CivitModelType.Hypernetwork),
        ["Controlnet"] = (SharedFolderType.ControlNet, CivitModelType.Controlnet),
        ["VAE"] = (SharedFolderType.VAE, CivitModelType.VAE),
        ["Upscaler"] = (SharedFolderType.ESRGAN, CivitModelType.Upscaler),
    };

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (IsLoading || string.IsNullOrWhiteSpace(RelativeUrl))
        {
            return;
        }

        await LoadModelAsync();
    }

    private async Task LoadModelAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;

        try
        {
            var response = await civArchiveApiClient.GetModelDetailsAsync(RelativeUrl);
            Model = response.Model;

            ModelDescriptionHtml = WrapHtml(response.Model.Description);
            PopulateVersionData(response.Model.Version);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateVersionData(CivArchiveModelVersion? version)
    {
        VersionDescriptionHtml = WrapHtml(version?.Description);
        HasDownloadUrl = !string.IsNullOrWhiteSpace(version?.DownloadUrl);

        Images.Clear();
        foreach (var image in version?.Images.Where(i => !string.IsNullOrWhiteSpace(i.Url)) ?? [])
        {
            Images.Add(image);
        }

        Files.Clear();
        foreach (var file in version?.Files ?? [])
        {
            Files.Add(file);
        }

        Mirrors.Clear();
        foreach (var mirror in version?.Mirrors ?? [])
        {
            Mirrors.Add(mirror);
        }
    }

    private static string WrapHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return $"""<html><body class="markdown-body">{html}</body></html>""";
    }

    [RelayCommand]
    private async Task ShowImageDialog(CivArchiveModelImage? image)
    {
        if (image?.Url is null)
        {
            return;
        }

        var currentIndex = Images.IndexOf(image);
        var imageSource = new ImageSource(new Uri(image.Url));

        await imageSource.GetBitmapAsync();

        var vm = vmFactory.Get<ImageViewerViewModel>();
        vm.ImageSource = imageSource;

        using var onNav = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(ctx =>
            {
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < Images.Count)
                        {
                            var newImage = Images[newIndex];
                            if (newImage.Url is null)
                            {
                                return;
                            }

                            var newSource = new ImageSource(new Uri(newImage.Url));
                            await newSource.GetBitmapAsync();
                            sender.ImageSource = newSource;
                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        await vm.GetDialog().ShowAsync();
    }

    [RelayCommand]
    private async Task SelectVersion(CivArchiveVersionReference? versionRef)
    {
        if (versionRef is null || string.IsNullOrWhiteSpace(versionRef.Href) || IsLoading)
        {
            return;
        }

        SelectedVersion = versionRef;
        RelativeUrl = versionRef.Href;
        await LoadModelAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (!navigationService.GoBack())
        {
            navigationService.NavigateTo<CheckpointBrowserViewModel>();
        }
    }

    [RelayCommand]
    private void OpenOnCivArchive()
    {
        ProcessRunner.OpenUrl(civArchiveApiClient.GetAbsoluteUri(RelativeUrl).ToString());
    }

    [RelayCommand]
    private async Task DownloadModel()
    {
        var version = Model?.Version;
        if (version?.DownloadUrl is not { } downloadUrl || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        if (!settingsManager.IsLibraryDirSet)
        {
            notificationService.Show("Download Failed", "Please set a library directory in settings first.");
            return;
        }

        var downloadUri = civArchiveApiClient.GetAbsoluteUri(downloadUrl);

        // Auto-determine download folder from model type
        var destinationDir = GetDefaultDownloadFolder();

        // Build filename: "{ModelName}_{VersionName}.safetensors"
        var modelName = Model?.Name ?? "model";
        var versionName = version.Name;
        var fileName = string.IsNullOrWhiteSpace(versionName)
            ? $"{modelName}.safetensors"
            : $"{modelName}_{versionName}.safetensors";

        // Sanitize filename
        fileName = Path.GetInvalidFileNameChars()
            .Aggregate(fileName, (current, c) => current.Replace(c, '_'));

        // Get preview image URI if available
        Uri? previewImageUri = null;
        var firstImage = version.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Url));
        if (firstImage?.Url is not null)
        {
            previewImageUri = new Uri(firstImage.Url);
        }

        // Build cm-info metadata
        var connectedModelInfo = BuildConnectedModelInfo(Model!, version);

        await modelImportService.DoCustomImport(
            downloadUri,
            fileName,
            destinationDir,
            previewImageUri,
            connectedModelInfo: connectedModelInfo,
            configureDownload: download =>
            {
                if (!string.IsNullOrWhiteSpace(version.Files.FirstOrDefault()?.Sha256))
                {
                    download.ExpectedHashSha256 = version.Files.First().Sha256;
                }
            }
        );

        notificationService.Show("Download Started", $"{fileName} will be saved to {destinationDir}");
    }

    private DirectoryPath GetDefaultDownloadFolder()
    {
        var modelType = Model?.Type;
        if (modelType is not null && ModelTypeMap.TryGetValue(modelType, out var mapping))
        {
            return new DirectoryPath(settingsManager.ModelsDirectory, mapping.Folder.GetStringValue());
        }

        return new DirectoryPath(settingsManager.ModelsDirectory);
    }

    private static ConnectedModelInfo BuildConnectedModelInfo(
        CivArchiveModelDetails model,
        CivArchiveModelVersion version
    )
    {
        var civitModelType = CivitModelType.Unknown;
        if (model.Type is not null && ModelTypeMap.TryGetValue(model.Type, out var mapping))
        {
            civitModelType = mapping.ModelType;
        }

        var primaryFile = version.Files.FirstOrDefault(f => f.IsPrimary) ?? version.Files.FirstOrDefault();

        return new ConnectedModelInfo
        {
            ModelName = model.Name,
            ModelDescription = model.Description ?? string.Empty,
            Nsfw = model.IsNsfw,
            Tags = model.Tags.ToArray(),
            ModelType = civitModelType,
            VersionName = version.Name,
            VersionDescription = version.Description,
            BaseModel = version.BaseModel,
            ImportedAt = DateTimeOffset.UtcNow,
            Hashes = new CivitFileHashes { SHA256 = primaryFile?.Sha256 },
            TrainedWords = version.Trigger.ToArray(),
            ThumbnailImageUrl = version.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Url))?.Url,
            Source = ConnectedModelSource.CivArchive,
            Stats = new CivitModelStats
            {
                DownloadCount = (int)model.DownloadCount,
                FavoriteCount = (int)model.FavoriteCount,
                CommentCount = (int)model.CommentCount,
                RatingCount = (int)model.RatingCount,
                Rating = model.Rating,
            },
        };
    }

    [RelayCommand]
    private void OpenVersionMirror(CivArchiveVersionMirror? mirror)
    {
        if (!string.IsNullOrWhiteSpace(mirror?.PlatformUrl))
        {
            ProcessRunner.OpenUrl(mirror.PlatformUrl);
        }
    }

    [RelayCommand]
    private void OpenFileMirror(CivArchiveFileMirror? mirror)
    {
        if (!string.IsNullOrWhiteSpace(mirror?.Url))
        {
            ProcessRunner.OpenUrl(mirror.Url);
        }
    }

    [RelayCommand]
    private async Task CopySha256(CivArchiveModelFile? file)
    {
        if (!string.IsNullOrWhiteSpace(file?.Sha256) && App.Clipboard is not null)
        {
            await App.Clipboard.SetTextAsync(file.Sha256);
        }
    }
}
