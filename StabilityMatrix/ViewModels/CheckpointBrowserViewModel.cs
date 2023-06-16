using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using System.Diagnostics;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Database;
using StabilityMatrix.Extensions;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Services;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ICivitApi civitApi;
    private readonly IDownloadService downloadService;
    private readonly ISnackbarService snackbarService;
    private readonly ISettingsManager settingsManager;
    private readonly ILiteDbContext liteDbContext;
    private const int MaxModelsPerPage = 14;
    private Stopwatch queryTimer = new();

    [ObservableProperty] private string? searchQuery;
    [ObservableProperty] private ObservableCollection<CheckpointBrowserCardViewModel>? modelCards;
    [ObservableProperty] private bool showNsfw;
    [ObservableProperty] private bool showMainLoadingSpinner;
    [ObservableProperty] private CivitPeriod selectedPeriod;
    [ObservableProperty] private CivitSortMode sortMode;
    [ObservableProperty] private CivitModelType selectedModelType;
    [ObservableProperty] private int currentPageNumber;
    [ObservableProperty] private int totalPages;
    [ObservableProperty] private bool hasSearched;
    [ObservableProperty] private bool canGoToNextPage;
    [ObservableProperty] private bool canGoToPreviousPage;
    [ObservableProperty] private bool isIndeterminate;

    public IEnumerable<CivitPeriod> AllCivitPeriods => Enum.GetValues(typeof(CivitPeriod)).Cast<CivitPeriod>();
    public IEnumerable<CivitSortMode> AllSortModes => Enum.GetValues(typeof(CivitSortMode)).Cast<CivitSortMode>();
    public IEnumerable<CivitModelType> AllModelTypes => Enum.GetValues(typeof(CivitModelType)).Cast<CivitModelType>();

    public CheckpointBrowserViewModel(
        ICivitApi civitApi, 
        IDownloadService downloadService, 
        ISnackbarService snackbarService, 
        ISettingsManager settingsManager,
        ILiteDbContext liteDbContext)
    {
        this.civitApi = civitApi;
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        this.settingsManager = settingsManager;
        this.liteDbContext = liteDbContext;
        
        SelectedPeriod = CivitPeriod.Month;
        SortMode = CivitSortMode.HighestRated;
        SelectedModelType = CivitModelType.All;
        HasSearched = false;
        CurrentPageNumber = 1;
        CanGoToPreviousPage = false;
        CanGoToNextPage = true;
    }

    /// <summary>
    /// Background update task
    /// </summary>
    private async Task CivitModelQuery(CivitModelsRequest request)
    {
        var queryText = request.Query;
        try
        {
            var modelsResponse = await civitApi.GetModels(request);
            var models = modelsResponse.Items;
            if (models is null)
            {
                Logger.Debug("CivitAI Query '{Text}' returned no results ({Elapsed} ms)", queryText, queryTimer.Elapsed.TotalMilliseconds);
                return;
            }
            Logger.Debug("CivitAI Query '{Text}' returned {Results} results ({Elapsed} ms)", queryText, models.Count, queryTimer.Elapsed.TotalMilliseconds);
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
        catch (ApiException e)
        {
            snackbarService.ShowSnackbarAsync("Please try again in a few minutes",
                "CivitAI can't be reached right now").SafeFireAndForget();
            Logger.Log(LogLevel.Error, e);
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
                .Select(model => new CheckpointBrowserCardViewModel(model, downloadService, snackbarService, settingsManager));
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

    /// <summary>
    /// Handler for database changes.
    /// Refreshes the model cards.
    /// </summary>
    private async Task OnDatabaseChanged()
    {
    }

    [RelayCommand]
    private async Task SearchModels()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        // Start timers and progress
        queryTimer.Restart();
        ShowMainLoadingSpinner = true;

        // Build request
        var modelRequest = new CivitModelsRequest
        {
            Query = SearchQuery,
            Limit = MaxModelsPerPage,
            Nsfw = ShowNsfw.ToString().ToLower(),
            Sort = SortMode,
            Period = SelectedPeriod,
            Page = CurrentPageNumber,
        };
        if (SelectedModelType != CivitModelType.All)
        {
            modelRequest.Types = new[] {SelectedModelType};
        }
        // See if query is cached
        var cachedQuery = await liteDbContext.CivitModelQueryCache
            .IncludeAll()
            .FindByIdAsync(ObjectHash.GetMd5Guid(modelRequest));
        // If cached, update model cards
        if (cachedQuery?.Items is not null && cachedQuery.Items.Any())
        {
            var elapsed = queryTimer.Elapsed.TotalMilliseconds;
            Logger.Debug("Using cached query for '{Text}' [{RequestHash}] ({Elapsed} ms)", SearchQuery, modelRequest.GetHashCode(), elapsed);
            UpdateModelCards(cachedQuery.Items, cachedQuery.Metadata);
            Logger.Debug("Updated model cards ({Elapsed} ms)", queryTimer.Elapsed.TotalMilliseconds - elapsed);
            
            // Start remote query (background mode)
            // Skip when last query was less than 2 min ago
            var diffMinutes = (DateTimeOffset.UtcNow - cachedQuery.InsertedAt)?.TotalMinutes;
            if (diffMinutes < 2)
            {
                Logger.Debug($"Last query was ({diffMinutes}) < 2 minutes ago, skipping remote query");
                return;
            }
            CivitModelQuery(modelRequest).SafeFireAndForget();
        }
        else
        {
            // Not cached, wait for remote query
            await CivitModelQuery(modelRequest);
        }
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

    partial void OnShowNsfwChanged(bool oldValue, bool newValue)
    {
        TrySearchAgain().SafeFireAndForget();
    }

    partial void OnSelectedPeriodChanged(CivitPeriod oldValue, CivitPeriod newValue)
    {
        TrySearchAgain().SafeFireAndForget();
    }

    partial void OnSortModeChanged(CivitSortMode oldValue, CivitSortMode newValue)
    {
        TrySearchAgain().SafeFireAndForget();
    }
    
    partial void OnSelectedModelTypeChanged(CivitModelType oldValue, CivitModelType newValue)
    {
        TrySearchAgain().SafeFireAndForget();
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
}
