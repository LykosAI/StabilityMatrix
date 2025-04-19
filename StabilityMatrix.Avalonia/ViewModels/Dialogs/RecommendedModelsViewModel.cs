using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Api.LykosAuthApi;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[RegisterTransient<RecommendedModelsViewModel>]
[ManagedService]
public partial class RecommendedModelsViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<RecommendedModelsViewModel> logger;
    private readonly IRecommendedModelsApi lykosApi;
    private readonly ICivitApi civitApi;
    private readonly ILiteDbContext liteDbContext;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly IDownloadService downloadService;
    private readonly IModelImportService modelImportService;

    // Single source cache for all models
    public SourceCache<RecommendedModelItemViewModel, int> AllRecommendedModelsCache { get; } =
        new(p => p.ModelVersion.Id);

    // Single observable collection bound to the cache
    public IObservableCollection<RecommendedModelItemViewModel> RecommendedModels { get; } =
        new ObservableCollectionExtended<RecommendedModelItemViewModel>();

    [ObservableProperty]
    private bool isLoading;

    public RecommendedModelsViewModel(
        ILogger<RecommendedModelsViewModel> logger,
        IRecommendedModelsApi lykosApi,
        ICivitApi civitApi,
        ILiteDbContext liteDbContext,
        ISettingsManager settingsManager,
        INotificationService notificationService,
        ITrackedDownloadService trackedDownloadService,
        IDownloadService downloadService,
        IModelImportService modelImportService
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
        this.modelImportService = modelImportService;

        // Bind the single collection to the cache
        AllRecommendedModelsCache
            .Connect()
            .DeferUntilLoaded()
            .Bind(RecommendedModels)
            .ObserveOn(SynchronizationContext.Current!) // Use Current! if nullability context allows
            .Subscribe();
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        IsLoading = true;
        AllRecommendedModelsCache.Clear(); // Clear cache before loading

        try
        {
            // Call the V2 endpoint
            var recommendedModelsResponse = await lykosApi.GetRecommendedModels();

            var allModels = recommendedModelsResponse
                .RecommendedModelsByCategory.SelectMany(kvp => kvp.Value) // Flatten the dictionary values (lists of models)
                .DistinctBy(m => m.Id) // Ensure models appearing in multiple categories are only added once
                .Select(model =>
                {
                    // Find the first non-Turbo/Lightning version, or default to the first version if none match
                    var suitableVersion =
                        model.ModelVersions?.FirstOrDefault(
                            x =>
                                !x.BaseModel.Contains("Turbo", StringComparison.OrdinalIgnoreCase)
                                && !x.BaseModel.Contains("Lightning", StringComparison.OrdinalIgnoreCase)
                                && x.Files != null
                                && x.Files.Any(f => f.Type == CivitFileType.Model) // Ensure there's a model file
                        )
                        ?? model.ModelVersions?.FirstOrDefault(
                            x => x.Files != null && x.Files.Any(f => f.Type == CivitFileType.Model)
                        );

                    if (suitableVersion == null)
                    {
                        logger.LogWarning(
                            "Model {ModelName} (ID: {ModelId}) has no suitable model version file.",
                            model.Name,
                            model.Id
                        );
                        return null; // Skip this model if no suitable version found
                    }

                    return new RecommendedModelItemViewModel
                    {
                        ModelVersion = suitableVersion,
                        Author = $"by {model.Creator?.Username}",
                        CivitModel = model
                    };
                })
                .Where(vm => vm != null); // Filter out nulls (models skipped due to no suitable version)

            AllRecommendedModelsCache.AddOrUpdate(allModels);
        }
        catch (ApiException apiEx)
        {
            logger.LogError(
                apiEx,
                "API Error fetching recommended models V2. Status: {StatusCode}",
                apiEx.StatusCode
            );
            notificationService.Show(
                "Failed to get recommended models",
                $"Could not reach the server. Please try again later. Error: {apiEx.StatusCode}"
            );
            OnCloseButtonClick();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get recommended models V2");
            notificationService.Show(
                "Failed to get recommended models",
                "An unexpected error occurred. Please try again later or check the Model Browser tab."
            );
            OnCloseButtonClick();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DoImport()
    {
        var selectedModels = RecommendedModels.Where(x => x.IsSelected).ToList(); // Use the single list

        if (!selectedModels.Any())
        {
            notificationService.Show("No Models Selected", "Please select at least one model to import.");
            return;
        }

        IsLoading = true; // Optionally show loading indicator during import

        int successCount = 0;
        int failCount = 0;

        foreach (var model in selectedModels)
        {
            // Get latest version file that is a Model type and marked primary, or fallback to first model file
            var modelFile =
                model.ModelVersion.Files?.FirstOrDefault(
                    f => f is { Type: CivitFileType.Model, IsPrimary: true }
                ) ?? model.ModelVersion.Files?.FirstOrDefault(f => f.Type == CivitFileType.Model);

            if (modelFile is null)
            {
                logger.LogWarning(
                    "Skipping import for {ModelName}: No suitable model file found in version {VersionId}.",
                    model.CivitModel.Name,
                    model.ModelVersion.Id
                );
                failCount++;
                continue; // Skip if no suitable file
            }

            try
            {
                var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
                var downloadDirectory = rootModelsDirectory.JoinDir(
                    model.CivitModel.Type.ConvertTo<SharedFolderType>().GetStringValue()
                );

                await modelImportService.DoImport(
                    model.CivitModel,
                    downloadDirectory,
                    model.ModelVersion,
                    modelFile
                );
                successCount++;
                model.IsSelected = false; // De-select after successful import start
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initiate import for model {ModelName}", model.CivitModel.Name);
                failCount++;
                // Consider notifying the user about the specific failure
                notificationService.Show(
                    "Import Failed",
                    $"Could not start import for {model.CivitModel.Name}."
                );
            }
        }

        IsLoading = false; // Hide loading indicator

        if (failCount == 0 && successCount > 0)
        {
            notificationService.Show(
                "Import Started",
                $"{successCount} model(s) added to the download queue."
            );
            // Optionally close the dialog after successful import initiation
            // OnCloseButtonClick();
        }
        else if (successCount > 0)
        {
            notificationService.Show(
                "Import Partially Started",
                $"{successCount} model(s) added to queue. {failCount} failed to start."
            );
        }
        else if (failCount > 0)
        {
            notificationService.Show(
                "Import Failed",
                $"Could not start import for {failCount} selected model(s)."
            );
        }
        // else: No models were actually selected or processed, already handled.
    }
}
