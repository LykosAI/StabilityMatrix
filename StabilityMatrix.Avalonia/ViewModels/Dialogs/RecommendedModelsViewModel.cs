using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
public partial class RecommendedModelsViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<RecommendedModelsViewModel> logger;
    private readonly ILykosAuthApi lykosApi;
    private readonly ICivitApi civitApi;
    private readonly ILiteDbContext liteDbContext;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly IDownloadService downloadService;
    public SourceCache<RecommendedModelItemViewModel, int> CivitModels { get; } = new(p => p.ModelVersion.Id);

    public IObservableCollection<RecommendedModelItemViewModel> Sd15Models { get; set; } =
        new ObservableCollectionExtended<RecommendedModelItemViewModel>();

    public IObservableCollection<RecommendedModelItemViewModel> SdxlModels { get; } =
        new ObservableCollectionExtended<RecommendedModelItemViewModel>();

    [ObservableProperty]
    private bool isLoading;

    public RecommendedModelsViewModel(
        ILogger<RecommendedModelsViewModel> logger,
        ILykosAuthApi lykosApi,
        ICivitApi civitApi,
        ILiteDbContext liteDbContext,
        ISettingsManager settingsManager,
        INotificationService notificationService,
        ITrackedDownloadService trackedDownloadService,
        IDownloadService downloadService
    )
    {
        this.logger = logger;
        this.lykosApi = lykosApi;
        this.civitApi = civitApi;
        this.liteDbContext = liteDbContext;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.trackedDownloadService = trackedDownloadService;
        this.downloadService = downloadService;

        CivitModels
            .Connect()
            .DeferUntilLoaded()
            .Filter(f => f.ModelVersion.BaseModel == "SD 1.5")
            .Bind(Sd15Models)
            .Subscribe();

        CivitModels
            .Connect()
            .DeferUntilLoaded()
            .Filter(f => f.ModelVersion.BaseModel == "SDXL 1.0" || f.ModelVersion.BaseModel == "Pony")
            .Bind(SdxlModels)
            .Subscribe();
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        IsLoading = true;

        try
        {
            var recommendedModels = await lykosApi.GetRecommendedModels();

            CivitModels.AddOrUpdate(
                recommendedModels.Items.Select(
                    model =>
                        new RecommendedModelItemViewModel
                        {
                            ModelVersion = model.ModelVersions.First(
                                x =>
                                    !x.BaseModel.Contains("Turbo", StringComparison.OrdinalIgnoreCase)
                                    && !x.BaseModel.Contains("Lightning", StringComparison.OrdinalIgnoreCase)
                            ),
                            Author = $"by {model.Creator?.Username}",
                            CivitModel = model
                        }
                )
            );
        }
        catch (ApiException e)
        {
            // hide dialog and show error msg
            logger.LogError(e, "Failed to get recommended models");
            notificationService.Show(
                "Failed to get recommended models",
                "Please try again later or check the Model Browser tab for more models."
            );
            OnCloseButtonClick();
        }

        IsLoading = false;
    }

    [RelayCommand]
    private async Task DoImport()
    {
        var selectedModels = SdxlModels.Where(x => x.IsSelected).Concat(Sd15Models.Where(x => x.IsSelected));

        foreach (var model in selectedModels)
        {
            // Get latest version file
            var modelFile = model.ModelVersion.Files?.FirstOrDefault(
                x => x is { Type: CivitFileType.Model, IsPrimary: true }
            );
            if (modelFile is null)
            {
                continue;
            }

            var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);

            var downloadDirectory = rootModelsDirectory.JoinDir(
                model.CivitModel.Type.ConvertTo<SharedFolderType>().GetStringValue()
            );
            // Folders might be missing if user didn't install any packages yet
            downloadDirectory.Create();

            var downloadPath = downloadDirectory.JoinFile(modelFile.Name);

            // Download model info and preview first
            var cmInfoPath = await SaveCmInfo(
                model.CivitModel,
                model.ModelVersion,
                modelFile,
                downloadDirectory
            );
            var previewImagePath = await SavePreviewImage(model.ModelVersion, downloadPath);

            // Create tracked download
            var download = trackedDownloadService.NewDownload(modelFile.DownloadUrl, downloadPath);

            // Add hash info
            download.ExpectedHashSha256 = modelFile.Hashes.SHA256;

            // Add files to cleanup list
            download.ExtraCleanupFileNames.Add(cmInfoPath);
            if (previewImagePath is not null)
            {
                download.ExtraCleanupFileNames.Add(previewImagePath);
            }

            // Add hash context action
            download.ContextAction = CivitPostDownloadContextAction.FromCivitFile(modelFile);

            download.Start();
        }
    }

    private static async Task<FilePath> SaveCmInfo(
        CivitModel model,
        CivitModelVersion modelVersion,
        CivitFile modelFile,
        DirectoryPath downloadDirectory
    )
    {
        var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Name);
        var modelInfo = new ConnectedModelInfo(model, modelVersion, modelFile, DateTime.UtcNow);

        await modelInfo.SaveJsonToDirectory(downloadDirectory, modelFileName);

        var jsonName = $"{modelFileName}.cm-info.json";
        return downloadDirectory.JoinFile(jsonName);
    }

    /// <summary>
    /// Saves the preview image to the same directory as the model file
    /// </summary>
    /// <param name="modelVersion"></param>
    /// <param name="modelFilePath"></param>
    /// <returns>The file path of the saved preview image</returns>
    private async Task<FilePath?> SavePreviewImage(CivitModelVersion modelVersion, FilePath modelFilePath)
    {
        // Skip if model has no images
        if (modelVersion.Images == null || modelVersion.Images.Count == 0)
        {
            return null;
        }

        var image = modelVersion.Images[0];
        var imageExtension = Path.GetExtension(image.Url).TrimStart('.');
        if (imageExtension is "jpg" or "jpeg" or "png")
        {
            var imageDownloadPath = modelFilePath.Directory!.JoinFile(
                $"{modelFilePath.NameWithoutExtension}.preview.{imageExtension}"
            );

            var imageTask = downloadService.DownloadToFileAsync(image.Url, imageDownloadPath);
            await notificationService.TryAsync(imageTask, "Could not download preview image");

            return imageDownloadPath;
        }

        return null;
    }
}
