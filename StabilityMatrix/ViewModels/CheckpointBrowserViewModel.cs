using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using System.Diagnostics;
using System.Net.Http;
using System.Windows.Data;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Helper;
using ApiException = Refit.ApiException;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ICivitApi civitApi;
    private readonly IDownloadService downloadService;
    private readonly ISnackbarService snackbarService;
    private readonly ISettingsManager settingsManager;
    private readonly IDialogFactory dialogFactory;
    private readonly ILiteDbContext liteDbContext;
    private const int MaxModelsPerPage = 14;

    [ObservableProperty] private ObservableCollection<CheckpointBrowserCardViewModel>? modelCards;
    [ObservableProperty] private ICollectionView? modelCardsView;

    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private bool showNsfw;
    [ObservableProperty] private bool showMainLoadingSpinner;
    [ObservableProperty] private CivitPeriod selectedPeriod = CivitPeriod.Month;
    [ObservableProperty] private CivitSortMode sortMode = CivitSortMode.HighestRated;
    [ObservableProperty] private CivitModelType selectedModelType = CivitModelType.Checkpoint;
    [ObservableProperty] private int currentPageNumber;
    [ObservableProperty] private int totalPages;
    [ObservableProperty] private bool hasSearched;
    [ObservableProperty] private bool canGoToNextPage;
    [ObservableProperty] private bool canGoToPreviousPage;
    [ObservableProperty] private bool isIndeterminate;
    [ObservableProperty] private bool noResultsFound;
    [ObservableProperty] private string noResultsText;
    
    public IEnumerable<CivitPeriod> AllCivitPeriods => Enum.GetValues(typeof(CivitPeriod)).Cast<CivitPeriod>();
    public IEnumerable<CivitSortMode> AllSortModes => Enum.GetValues(typeof(CivitSortMode)).Cast<CivitSortMode>();

    public IEnumerable<CivitModelType> AllModelTypes => Enum.GetValues(typeof(CivitModelType))
        .Cast<CivitModelType>()
        .Where(t => t == CivitModelType.All || t.ConvertTo<SharedFolderType>() > 0)
        .OrderBy(t => t.ToString());

    public CheckpointBrowserViewModel(
        ICivitApi civitApi, 
        IDownloadService downloadService, 
        ISnackbarService snackbarService, 
        ISettingsManager settingsManager,
        IDialogFactory dialogFactory,
        ILiteDbContext liteDbContext)
    {
        this.civitApi = civitApi;
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.liteDbContext = liteDbContext;
        
        CurrentPageNumber = 1;
        CanGoToNextPage = true;
    }

    public void OnLoaded()
    {
        var searchOptions = settingsManager.Settings.ModelSearchOptions;
        
        SelectedPeriod = searchOptions?.SelectedPeriod ?? CivitPeriod.Month;
        SortMode = searchOptions?.SortMode ?? CivitSortMode.HighestRated;
        SelectedModelType = searchOptions?.SelectedModelType ?? CivitModelType.Checkpoint;
        
        ShowNsfw = settingsManager.Settings.ModelBrowserNsfwEnabled;
        
        settingsManager.RelayPropertyFor(this, model => model.ShowNsfw,
            settings => settings.ModelBrowserNsfwEnabled);
    }

    /// <summary>
    /// Filter predicate for model cards
    /// </summary>
    private bool FilterModelCardsPredicate(object? item)
    {
        if (item is not CheckpointBrowserCardViewModel card) return false;
        return !card.CivitModel.Nsfw || ShowNsfw;
    }

    /// <summary>
    /// Background update task
    /// </summary>
    private async Task CivitModelQuery(CivitModelsRequest request)
    {
        var timer = Stopwatch.StartNew();
        var queryText = request.Query;
        try
        {
            var modelsResponse = await civitApi.GetModels(request);
            var models = modelsResponse.Items;
            if (models is null)
            {
                Logger.Debug("CivitAI Query {Text} returned no results (in {Elapsed:F1} s)",
                    queryText, timer.Elapsed.TotalSeconds);
                return;
            }

            Logger.Debug("CivitAI Query {Text} returned {Results} results (in {Elapsed:F1} s)",
                queryText, models.Count, timer.Elapsed.TotalSeconds);

            var unknown = models.Where(m => m.Type == CivitModelType.Unknown).ToList();
            if (unknown.Any())
            {
                var names = unknown.Select(m => m.Name).ToList();
                Logger.Warn("Excluded {Unknown} unknown model types: {Models}", unknown.Count,
                    names);
            }

            // Filter out unknown model types and archived/taken-down models
            models = models.Where(m => m.Type.ConvertTo<SharedFolderType>() > 0)
                .Where(m => m.Mode == null).ToList();

            // Database update calls will invoke `OnModelsUpdated`
            // Add to database
            await liteDbContext.UpsertCivitModelAsync(models);
            // Add as cache entry
            var cacheNew = await liteDbContext.UpsertCivitModelQueryCacheEntryAsync(new()
            {
                Id = ObjectHash.GetMd5Guid(request),
                InsertedAt = DateTimeOffset.UtcNow,
                Request = request,
                Items = models,
                Metadata = modelsResponse.Metadata
            });

            if (cacheNew)
            {
                Logger.Debug("New cache entry, updating model cards");
                UpdateModelCards(models, modelsResponse.Metadata);
            }
            else
            {
                Logger.Debug("Cache entry already exists, not updating model cards");
            }
        }
        catch (OperationCanceledException)
        {
            snackbarService.ShowSnackbarAsync("Request to CivitAI timed out",
                "Please try again in a few minutes").SafeFireAndForget();
            Logger.Warn($"CivitAI query timed out ({request})");
        }
        catch (HttpRequestException e)
        {
            snackbarService.ShowSnackbarAsync("CivitAI can't be reached right now",
                "Please try again in a few minutes").SafeFireAndForget();
            Logger.Warn(e, $"CivitAI query HttpRequestException ({request})");
        }
        catch (ApiException e)
        {
            snackbarService.ShowSnackbarAsync("CivitAI can't be reached right now",
                "Please try again in a few minutes").SafeFireAndForget();
            Logger.Warn(e, $"CivitAI query ApiException ({request})");
        }
        catch (Exception e)
        {
            snackbarService.ShowSnackbarAsync($"Please try again in a few minutes",
                $"Unknown exception during CivitAI query: {e.GetType().Name}").SafeFireAndForget();
            Logger.Error(e, $"CivitAI query unknown exception ({request})");
        }
        finally
        {
            ShowMainLoadingSpinner = false;
            UpdateResultsText();
        }
    }
    
    /// <summary>
    /// Updates model cards using api response object.
    /// </summary>
    private void UpdateModelCards(IEnumerable<CivitModel>? models, CivitMetadata? metadata) 
    {
        if (models is null)
        {
            ModelCards?.Clear();
        }
        else
        {
            var updateCards = models
                .Select(model => new CheckpointBrowserCardViewModel(model, 
                    downloadService, snackbarService, settingsManager, dialogFactory));
            ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>(updateCards);
        }
        TotalPages = metadata?.TotalPages ?? 1;
        CanGoToPreviousPage = CurrentPageNumber > 1;
        CanGoToNextPage = CurrentPageNumber < TotalPages;
        // Status update
        ShowMainLoadingSpinner = false;
        IsIndeterminate = false;
        HasSearched = true;
    }

    private string previousSearchQuery = string.Empty;

    [RelayCommand]
    private async Task SearchModels()
    {
        var timer = Stopwatch.StartNew();
        
        if (SearchQuery != previousSearchQuery)
        {
            // Reset page number
            CurrentPageNumber = 1;
            previousSearchQuery = SearchQuery;
        }
        
        // Build request
        var modelRequest = new CivitModelsRequest
        {
            Limit = MaxModelsPerPage,
            Nsfw = "true", // Handled by local view filter
            Sort = SortMode,
            Period = SelectedPeriod,
            Page = CurrentPageNumber
        };

        if (SearchQuery.StartsWith("#"))
        {
            modelRequest.Tag = SearchQuery[1..];
        }
        else if (SearchQuery.StartsWith("@"))
        {
            modelRequest.Username = SearchQuery[1..];
        }
        else
        {
            modelRequest.Query = SearchQuery;
        }
        
        if (SelectedModelType != CivitModelType.All)
        {
            modelRequest.Types = new[] {SelectedModelType};
        }
        
        // See if query is cached
        var cachedQuery = await liteDbContext.CivitModelQueryCache
            .IncludeAll()
            .FindByIdAsync(ObjectHash.GetMd5Guid(modelRequest));
        
        // If cached, update model cards
        if (cachedQuery is not null)
        {
            var elapsed = timer.Elapsed;
            Logger.Debug("Using cached query for {Text} [{RequestHash}] (in {Elapsed:F1} s)", 
                SearchQuery, modelRequest.GetHashCode(), elapsed.TotalSeconds);
            UpdateModelCards(cachedQuery.Items, cachedQuery.Metadata);

            // Start remote query (background mode)
            // Skip when last query was less than 2 min ago
            var timeSinceCache = DateTimeOffset.UtcNow - cachedQuery.InsertedAt;
            if (timeSinceCache?.TotalMinutes >= 2)
            {
                CivitModelQuery(modelRequest).SafeFireAndForget();
                Logger.Debug(
                    "Cached query was more than 2 minutes ago ({Seconds:F0} s), updating cache with remote query",
                    timeSinceCache.Value.TotalSeconds);
            }
        }
        else
        {
            // Not cached, wait for remote query
            ShowMainLoadingSpinner = true;
            await CivitModelQuery(modelRequest);
        }
        
        UpdateResultsText();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        if (CurrentPageNumber == 1) return;

        CurrentPageNumber--;
        await TrySearchAgain(false);
    }

    [RelayCommand]
    private async Task NextPage()
    {
        CurrentPageNumber++;
        await TrySearchAgain(false);
    }

    // On changes to ModelCards, update the view source
    partial void OnModelCardsChanged(ObservableCollection<CheckpointBrowserCardViewModel>? value)
    {
        if (value is null)
        {
            ModelCardsView = null;
        }
        // Create new view
        var view = new ListCollectionView(value!)
        {
            Filter = FilterModelCardsPredicate,
        };
        ModelCardsView = view;
    }
    
    partial void OnShowNsfwChanged(bool value)
    {
        settingsManager.Transaction(s => s.ModelBrowserNsfwEnabled = value);
        ModelCardsView?.Refresh();
        
        if (!HasSearched) 
            return;
        
        UpdateResultsText();
    }

    partial void OnSelectedPeriodChanged(CivitPeriod oldValue, CivitPeriod newValue)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s => s.ModelSearchOptions = new ModelSearchOptions(
                newValue, SortMode, SelectedModelType));
    }

    partial void OnSortModeChanged(CivitSortMode oldValue, CivitSortMode newValue)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s => s.ModelSearchOptions = new ModelSearchOptions(
                SelectedPeriod, newValue, SelectedModelType));
    }
    
    partial void OnSelectedModelTypeChanged(CivitModelType oldValue, CivitModelType newValue)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s => s.ModelSearchOptions = new ModelSearchOptions(
            SelectedPeriod, SortMode, newValue));
    }

    private async Task TrySearchAgain(bool shouldUpdatePageNumber = true)
    {
        if (!HasSearched) return;
        ModelCards?.Clear();

        if (shouldUpdatePageNumber)
        {
            CurrentPageNumber = 1;
        }

        // execute command instead of calling method directly so that the IsRunning property gets updated
        await SearchModelsCommand.ExecuteAsync(null);
    }

    private void UpdateResultsText()
    {
        NoResultsFound = ModelCardsView?.IsEmpty ?? true;
        NoResultsText = ModelCards?.Count > 0
            ? $"{ModelCards.Count} results hidden by filters"
            : "No results found";
    }
}
