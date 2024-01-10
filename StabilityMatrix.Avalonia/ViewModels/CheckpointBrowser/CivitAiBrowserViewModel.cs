using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using LiteDB.Async;
using NLog;
using Refit;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using Notification = Avalonia.Controls.Notifications.Notification;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivitAiBrowserPage))]
[Singleton]
public partial class CivitAiBrowserViewModel : TabViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ICivitApi civitApi;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly ILiteDbContext liteDbContext;
    private readonly INotificationService notificationService;
    private const int MaxModelsPerPage = 20;
    private LRUCache<
        int /* model id */
        ,
        CheckpointBrowserCardViewModel
    > cache = new(50);

    [ObservableProperty]
    private ObservableCollection<CheckpointBrowserCardViewModel>? modelCards;

    [ObservableProperty]
    private DataGridCollectionView? modelCardsView;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool showNsfw;

    [ObservableProperty]
    private bool showMainLoadingSpinner;

    [ObservableProperty]
    private CivitPeriod selectedPeriod = CivitPeriod.AllTime;

    [ObservableProperty]
    private CivitSortMode sortMode = CivitSortMode.HighestRated;

    [ObservableProperty]
    private CivitModelType selectedModelType = CivitModelType.Checkpoint;

    [ObservableProperty]
    private int currentPageNumber;

    [ObservableProperty]
    private int totalPages;

    [ObservableProperty]
    private bool hasSearched;

    [ObservableProperty]
    private bool canGoToNextPage;

    [ObservableProperty]
    private bool canGoToPreviousPage;

    [ObservableProperty]
    private bool canGoToFirstPage;

    [ObservableProperty]
    private bool canGoToLastPage;

    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private bool noResultsFound;

    [ObservableProperty]
    private string noResultsText = string.Empty;

    [ObservableProperty]
    private string selectedBaseModelType = "All";

    [ObservableProperty]
    private bool showSantaHats = true;

    private List<CheckpointBrowserCardViewModel> allModelCards = new();

    public IEnumerable<CivitPeriod> AllCivitPeriods =>
        Enum.GetValues(typeof(CivitPeriod)).Cast<CivitPeriod>();
    public IEnumerable<CivitSortMode> AllSortModes =>
        Enum.GetValues(typeof(CivitSortMode)).Cast<CivitSortMode>();

    public IEnumerable<CivitModelType> AllModelTypes =>
        Enum.GetValues(typeof(CivitModelType))
            .Cast<CivitModelType>()
            .Where(t => t == CivitModelType.All || t.ConvertTo<SharedFolderType>() > 0)
            .OrderBy(t => t.ToString());

    public IEnumerable<string> BaseModelOptions =>
        Enum.GetValues<CivitBaseModelType>().Select(t => t.GetStringValue());

    public CivitAiBrowserViewModel(
        ICivitApi civitApi,
        IDownloadService downloadService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> dialogFactory,
        ILiteDbContext liteDbContext,
        INotificationService notificationService
    )
    {
        this.civitApi = civitApi;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.liteDbContext = liteDbContext;
        this.notificationService = notificationService;

        CurrentPageNumber = 1;
        CanGoToNextPage = true;
        CanGoToLastPage = true;

        Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName == nameof(CurrentPageNumber))
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select<EventPattern<PropertyChangedEventArgs>, int>(_ => CurrentPageNumber)
            .Where(page => page <= TotalPages && page > 0)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => TrySearchAgain(false).SafeFireAndForget(), err => Logger.Error(err));
    }

    public override void OnLoaded()
    {
        if (Design.IsDesignMode)
            return;

        var searchOptions = settingsManager.Settings.ModelSearchOptions;

        // Fix SelectedModelType if someone had selected the obsolete "Model" option
        if (searchOptions is { SelectedModelType: CivitModelType.Model })
        {
            settingsManager.Transaction(
                s =>
                    s.ModelSearchOptions = new ModelSearchOptions(
                        SelectedPeriod,
                        SortMode,
                        CivitModelType.Checkpoint,
                        SelectedBaseModelType
                    )
            );
            searchOptions = settingsManager.Settings.ModelSearchOptions;
        }

        SelectedPeriod = searchOptions?.SelectedPeriod ?? CivitPeriod.Month;
        SortMode = searchOptions?.SortMode ?? CivitSortMode.HighestRated;
        SelectedModelType = searchOptions?.SelectedModelType ?? CivitModelType.Checkpoint;
        SelectedBaseModelType = searchOptions?.SelectedBaseModelType ?? "All";

        ShowNsfw = settingsManager.Settings.ModelBrowserNsfwEnabled;

        settingsManager.RelayPropertyFor(
            this,
            model => model.ShowNsfw,
            settings => settings.ModelBrowserNsfwEnabled
        );
    }

    /// <summary>
    /// Filter predicate for model cards
    /// </summary>
    private bool FilterModelCardsPredicate(object? item)
    {
        if (item is not CheckpointBrowserCardViewModel card)
            return false;
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
                Logger.Debug(
                    "CivitAI Query {Text} returned no results (in {Elapsed:F1} s)",
                    queryText,
                    timer.Elapsed.TotalSeconds
                );
                return;
            }

            Logger.Debug(
                "CivitAI Query {Text} returned {Results} results (in {Elapsed:F1} s)",
                queryText,
                models.Count,
                timer.Elapsed.TotalSeconds
            );

            var unknown = models.Where(m => m.Type == CivitModelType.Unknown).ToList();
            if (unknown.Any())
            {
                var names = unknown.Select(m => m.Name).ToList();
                Logger.Warn("Excluded {Unknown} unknown model types: {Models}", unknown.Count, names);
            }

            // Filter out unknown model types and archived/taken-down models
            models = models
                .Where(m => m.Type.ConvertTo<SharedFolderType>() > 0)
                .Where(m => m.Mode == null)
                .ToList();

            // Database update calls will invoke `OnModelsUpdated`
            // Add to database
            await liteDbContext.UpsertCivitModelAsync(models);
            // Add as cache entry
            var cacheNew = await liteDbContext.UpsertCivitModelQueryCacheEntryAsync(
                new()
                {
                    Id = ObjectHash.GetMd5Guid(request),
                    InsertedAt = DateTimeOffset.UtcNow,
                    Request = request,
                    Items = models,
                    Metadata = modelsResponse.Metadata
                }
            );

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
            notificationService.Show(
                new Notification("Request to CivitAI timed out", "Please try again in a few minutes")
            );
            Logger.Warn($"CivitAI query timed out ({request})");
        }
        catch (HttpRequestException e)
        {
            notificationService.Show(
                new Notification("CivitAI can't be reached right now", "Please try again in a few minutes")
            );
            Logger.Warn(e, $"CivitAI query HttpRequestException ({request})");
        }
        catch (ApiException e)
        {
            notificationService.Show(
                new Notification("CivitAI can't be reached right now", "Please try again in a few minutes")
            );
            Logger.Warn(e, $"CivitAI query ApiException ({request})");
        }
        catch (Exception e)
        {
            notificationService.Show(
                new Notification(
                    "CivitAI can't be reached right now",
                    $"Unknown exception during CivitAI query: {e.GetType().Name}"
                )
            );
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
                .Select(model =>
                {
                    var cachedViewModel = cache.Get(model.Id);
                    if (cachedViewModel != null)
                    {
                        if (!cachedViewModel.IsImporting)
                        {
                            cache.Remove(model.Id);
                        }

                        return cachedViewModel;
                    }

                    var newCard = dialogFactory.Get<CheckpointBrowserCardViewModel>(vm =>
                    {
                        vm.CivitModel = model;
                        vm.OnDownloadStart = viewModel =>
                        {
                            if (cache.Get(viewModel.CivitModel.Id) != null)
                                return;
                            cache.Add(viewModel.CivitModel.Id, viewModel);
                        };

                        return vm;
                    });

                    return newCard;
                })
                .ToList();

            allModelCards = updateCards;

            var filteredCards = updateCards.Where(FilterModelCardsPredicate);
            if (SortMode == CivitSortMode.Installed)
            {
                filteredCards = filteredCards.OrderByDescending(x => x.UpdateCardText == "Update Available");
            }

            ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>(filteredCards);
        }
        TotalPages = metadata?.TotalPages ?? 1;
        CanGoToFirstPage = CurrentPageNumber != 1;
        CanGoToPreviousPage = CurrentPageNumber > 1;
        CanGoToNextPage = CurrentPageNumber < TotalPages;
        CanGoToLastPage = CurrentPageNumber != TotalPages;
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
            modelRequest.Types = [SelectedModelType];
        }

        if (SelectedBaseModelType != "All")
        {
            modelRequest.BaseModel = SelectedBaseModelType;
        }

        if (SortMode == CivitSortMode.Installed)
        {
            var connectedModels = CheckpointFile
                .GetAllCheckpointFiles(settingsManager.ModelsDirectory)
                .Where(c => c.IsConnectedModel);

            modelRequest.CommaSeparatedModelIds = string.Join(
                ",",
                connectedModels.Select(c => c.ConnectedModel!.ModelId).GroupBy(m => m).Select(g => g.First())
            );
            modelRequest.Sort = null;
            modelRequest.Period = null;
        }
        else if (SortMode == CivitSortMode.Favorites)
        {
            var favoriteModels = settingsManager.Settings.FavoriteModels;

            if (!favoriteModels.Any())
            {
                notificationService.Show(
                    "No Favorites",
                    "You have not added any models to your Favorites.",
                    NotificationType.Error
                );
                return;
            }

            modelRequest.CommaSeparatedModelIds = string.Join(",", favoriteModels);
            modelRequest.Sort = null;
            modelRequest.Period = null;
        }

        // See if query is cached
        CivitModelQueryCacheEntry? cachedQuery = null;

        try
        {
            cachedQuery = await liteDbContext
                .CivitModelQueryCache.IncludeAll()
                .FindByIdAsync(ObjectHash.GetMd5Guid(modelRequest));
        }
        catch (Exception e)
        {
            // Suppress 'Training_Data' enum not found exceptions
            // Caused by enum name change
            // Ignore to do a new search to overwrite the cache
            if (
                !(
                    e is LiteException or LiteAsyncException
                    && e.InnerException is ArgumentException inner
                    && inner.Message.Contains("Training_Data")
                )
            )
            {
                // Otherwise log error
                Logger.Error(e, "Error while querying CivitModelQueryCache");
            }
        }

        // If cached, update model cards
        if (cachedQuery is not null)
        {
            var elapsed = timer.Elapsed;
            Logger.Debug(
                "Using cached query for {Text} [{RequestHash}] (in {Elapsed:F1} s)",
                SearchQuery,
                modelRequest.GetHashCode(),
                elapsed.TotalSeconds
            );
            UpdateModelCards(cachedQuery.Items, cachedQuery.Metadata);

            // Start remote query (background mode)
            // Skip when last query was less than 2 min ago
            var timeSinceCache = DateTimeOffset.UtcNow - cachedQuery.InsertedAt;
            if (timeSinceCache?.TotalMinutes >= 2)
            {
                CivitModelQuery(modelRequest).SafeFireAndForget();
                Logger.Debug(
                    "Cached query was more than 2 minutes ago ({Seconds:F0} s), updating cache with remote query",
                    timeSinceCache.Value.TotalSeconds
                );
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

    public void FirstPage()
    {
        CurrentPageNumber = 1;
    }

    public void PreviousPage()
    {
        if (CurrentPageNumber == 1)
            return;

        CurrentPageNumber--;
    }

    public void NextPage()
    {
        if (CurrentPageNumber == TotalPages)
            return;

        CurrentPageNumber++;
    }

    public void LastPage()
    {
        CurrentPageNumber = TotalPages;
    }

    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    partial void OnShowNsfwChanged(bool value)
    {
        settingsManager.Transaction(s => s.ModelBrowserNsfwEnabled, value);
        // ModelCardsView?.Refresh();
        var updateCards = allModelCards.Where(FilterModelCardsPredicate);
        ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>(updateCards);

        if (!HasSearched)
            return;

        UpdateResultsText();
    }

    partial void OnSelectedPeriodChanged(CivitPeriod value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(
            s =>
                s.ModelSearchOptions = new ModelSearchOptions(
                    value,
                    SortMode,
                    SelectedModelType,
                    SelectedBaseModelType
                )
        );
    }

    partial void OnSortModeChanged(CivitSortMode value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(
            s =>
                s.ModelSearchOptions = new ModelSearchOptions(
                    SelectedPeriod,
                    value,
                    SelectedModelType,
                    SelectedBaseModelType
                )
        );
    }

    partial void OnSelectedModelTypeChanged(CivitModelType value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(
            s =>
                s.ModelSearchOptions = new ModelSearchOptions(
                    SelectedPeriod,
                    SortMode,
                    value,
                    SelectedBaseModelType
                )
        );
    }

    partial void OnSelectedBaseModelTypeChanged(string value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(
            s =>
                s.ModelSearchOptions = new ModelSearchOptions(
                    SelectedPeriod,
                    SortMode,
                    SelectedModelType,
                    value
                )
        );
    }

    private async Task TrySearchAgain(bool shouldUpdatePageNumber = true)
    {
        if (!HasSearched)
            return;
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
        NoResultsFound = ModelCards?.Count <= 0;
        NoResultsText =
            allModelCards.Count > 0 ? $"{allModelCards.Count} results hidden by filters" : "No results found";
    }

    public override string Header => Resources.Label_CivitAi;
}
