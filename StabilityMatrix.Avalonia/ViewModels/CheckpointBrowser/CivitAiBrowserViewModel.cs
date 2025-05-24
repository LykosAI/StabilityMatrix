using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Text.Json;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using NLog;
using Refit;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Notification = Avalonia.Controls.Notifications.Notification;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivitAiBrowserPage))]
[RegisterSingleton<CivitAiBrowserViewModel>]
public sealed partial class CivitAiBrowserViewModel : TabViewModelBase, IInfinitelyScroll
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly CivitCompatApiManager civitApi;
    private readonly ISettingsManager settingsManager;
    private readonly ILiteDbContext liteDbContext;
    private readonly IConnectedServiceManager connectedServiceManager;
    private readonly INotificationService notificationService;
    private readonly ICivitBaseModelTypeService baseModelTypeService;
    private bool dontSearch = false;

    private readonly SourceCache<OrderedValue<CivitModel>, int> modelCache = new(static ov => ov.Value.Id);

    [ObservableProperty]
    private IObservableCollection<CheckpointBrowserCardViewModel> modelCards =
        new ObservableCollectionExtended<CheckpointBrowserCardViewModel>();

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
    private bool hasSearched;

    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private bool noResultsFound;

    [ObservableProperty]
    private string noResultsText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> selectedBaseModels = [];

    [ObservableProperty]
    private bool showSantaHats = true;

    [ObservableProperty]
    private string? nextPageCursor;

    [ObservableProperty]
    private bool hideInstalledModels;

    [ObservableProperty]
    private bool hideEarlyAccessModels;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsResizeFactor))]
    private double resizeFactor;

    private readonly SourceCache<string, string> baseModelCache = new(static s => s);

    [ObservableProperty]
    private IObservableCollection<BaseModelOptionViewModel> allBaseModels =
        new ObservableCollectionExtended<BaseModelOptionViewModel>();

    [ObservableProperty]
    private bool civitUseDiscoveryApi;

    public bool UseLocalCache => true;

    public double StatsResizeFactor => Math.Clamp(ResizeFactor, 0.75d, 1.25d);

    public IEnumerable<CivitPeriod> AllCivitPeriods =>
        Enum.GetValues(typeof(CivitPeriod)).Cast<CivitPeriod>();
    public IEnumerable<CivitSortMode> AllSortModes =>
        Enum.GetValues(typeof(CivitSortMode)).Cast<CivitSortMode>();

    public IEnumerable<CivitModelType> AllModelTypes =>
        Enum.GetValues(typeof(CivitModelType))
            .Cast<CivitModelType>()
            .Where(t => t == CivitModelType.All || t.ConvertTo<SharedFolderType>() > 0)
            .OrderBy(t => t.ToString());

    public string ClearButtonText =>
        SelectedBaseModels.Count == AllBaseModels.Count
            ? Resources.Action_ClearSelection
            : Resources.Action_SelectAll;

    public bool ShowFilterNumber =>
        SelectedBaseModels.Count > 0 && SelectedBaseModels.Count < AllBaseModels.Count;

    public CivitAiBrowserViewModel(
        CivitCompatApiManager civitApi,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> dialogFactory,
        ILiteDbContext liteDbContext,
        IConnectedServiceManager connectedServiceManager,
        INotificationService notificationService,
        ICivitBaseModelTypeService baseModelTypeService
    )
    {
        this.civitApi = civitApi;
        this.settingsManager = settingsManager;
        this.liteDbContext = liteDbContext;
        this.connectedServiceManager = connectedServiceManager;
        this.notificationService = notificationService;
        this.baseModelTypeService = baseModelTypeService;

        EventManager.Instance.NavigateAndFindCivitModelRequested += OnNavigateAndFindCivitModelRequested;

        var settingsSelectedBaseModels = settingsManager.Settings.SelectedCivitBaseModels;

        var filterPredicate = Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x =>
                x.EventArgs.PropertyName
                    is nameof(HideInstalledModels)
                        or nameof(ShowNsfw)
                        or nameof(HideEarlyAccessModels)
            )
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(_ => (Func<CheckpointBrowserCardViewModel, bool>)FilterModelCardsPredicate)
            .StartWith(FilterModelCardsPredicate)
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        var sortPredicate = SortExpressionComparer<CheckpointBrowserCardViewModel>.Ascending(static x =>
            x.Order
        );

        AddDisposable(
            modelCache
                .Connect()
                .DeferUntilLoaded()
                .Transform(ov =>
                    dialogFactory.Get<CheckpointBrowserCardViewModel>(vm =>
                    {
                        vm.CivitModel = ov.Value;
                        vm.Order = ov.Order;
                        return vm;
                    })
                )
                .DisposeMany()
                .Filter(filterPredicate)
                .SortAndBind(ModelCards, sortPredicate)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe()
        );

        AddDisposable(
            baseModelCache
                .Connect()
                .DeferUntilLoaded()
                .Transform(baseModel => new BaseModelOptionViewModel
                {
                    ModelType = baseModel,
                    IsSelected = settingsSelectedBaseModels.Contains(baseModel),
                })
                .Bind(AllBaseModels)
                .WhenPropertyChanged(p => p.IsSelected)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(next =>
                {
                    if (next.Sender.IsSelected)
                        SelectedBaseModels.Add(next.Sender.ModelType);
                    else
                        SelectedBaseModels.Remove(next.Sender.ModelType);

                    OnPropertyChanged(nameof(ClearButtonText));
                    OnPropertyChanged(nameof(SelectedBaseModels));
                    OnPropertyChanged(nameof(ShowFilterNumber));
                })
        );

        if (Design.IsDesignMode)
            return;

        var settingsTransactionObservable = this.WhenPropertyChanged(x => x.SelectedBaseModels)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ =>
            {
                if (!settingsManager.IsLibraryDirSet)
                    return;

                settingsManager.Transaction(settings =>
                    settings.SelectedCivitBaseModels = SelectedBaseModels.ToList()
                );

                if (!dontSearch)
                {
                    TrySearchAgain().SafeFireAndForget();
                }
            });

        AddDisposable(settingsTransactionObservable);

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                model => model.ShowNsfw,
                settings => settings.ModelBrowserNsfwEnabled,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                model => model.HideInstalledModels,
                settings => settings.HideInstalledModelsInModelBrowser,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                model => model.ResizeFactor,
                settings => settings.CivitBrowserResizeFactor,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                model => model.HideEarlyAccessModels,
                settings => settings.HideEarlyAccessModels,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                model => model.CivitUseDiscoveryApi,
                settings => settings.CivitUseDiscoveryApi,
                true
            )
        );

        EventManager.Instance.NavigateAndFindCivitAuthorRequested += OnNavigateAndFindCivitAuthorRequested;
    }

    private void OnNavigateAndFindCivitAuthorRequested(object? sender, string? e)
    {
        if (string.IsNullOrWhiteSpace(e))
            return;

        SearchQuery = $"@{e}";
        SearchModelsCommand.ExecuteAsync(false).SafeFireAndForget();
    }

    private void OnNavigateAndFindCivitModelRequested(object? sender, int e)
    {
        if (e <= 0)
            return;

        SearchQuery = $"$#{e}";
        SearchModelsCommand.ExecuteAsync(false).SafeFireAndForget();
    }

    public override void OnLoaded()
    {
        if (Design.IsDesignMode)
            return;

        var searchOptions = settingsManager.Settings.ModelSearchOptions;

        // Fix SelectedModelType if someone had selected the obsolete "Model" option
        if (searchOptions is { SelectedModelType: CivitModelType.Model })
        {
            settingsManager.Transaction(s =>
                s.ModelSearchOptions = new ModelSearchOptions(
                    SelectedPeriod,
                    SortMode,
                    CivitModelType.Checkpoint,
                    string.Empty
                )
            );
            searchOptions = settingsManager.Settings.ModelSearchOptions;
        }

        SelectedPeriod = searchOptions?.SelectedPeriod ?? CivitPeriod.AllTime;
        SortMode = searchOptions?.SortMode ?? CivitSortMode.HighestRated;
        SelectedModelType = searchOptions?.SelectedModelType ?? CivitModelType.Checkpoint;

        base.OnLoaded();
    }

    protected override async Task OnInitialLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        await base.OnInitialLoadedAsync();

        if (settingsManager.Settings.AutoLoadCivitModels)
        {
            await SearchModelsCommand.ExecuteAsync(false);
        }

        var baseModels = await baseModelTypeService.GetBaseModelTypes(includeAllOption: false);
        if (baseModels.Count == 0)
        {
            return;
        }

        dontSearch = true;
        baseModelCache.AddOrUpdate(baseModels);
        dontSearch = false;
    }

    /// <summary>
    /// Filter predicate for model cards
    /// </summary>
    private bool FilterModelCardsPredicate(CheckpointBrowserCardViewModel card)
    {
        if (HideInstalledModels && card.UpdateCardText == "Installed")
            return false;

        if (
            HideEarlyAccessModels
            && card.CivitModel.ModelVersions != null
            && card.CivitModel.ModelVersions.All(x => x.Availability == "EarlyAccess")
        )
            return false;

        return !card.CivitModel.Nsfw || ShowNsfw;
    }

    [RelayCommand]
    private async Task OnUseDiscoveryToggle()
    {
        if (CivitUseDiscoveryApi)
        {
            CivitUseDiscoveryApi = false;
        }
        else
        {
            if (!await connectedServiceManager.PromptEnableCivitUseDiscoveryApi())
                return;

            CivitUseDiscoveryApi = true;
        }

        // Reset cache in case model differences
        Logger.Info("Toggled Discovery API, clearing cache");

        await liteDbContext.CivitModels.DeleteAllAsync();
        await liteDbContext.CivitModelVersions.DeleteAllAsync();
        var items = await liteDbContext.CivitModelQueryCache.DeleteAllAsync();

        Logger.Info("Deleted {Count} Civit model query cache entries", items);
    }

    /// <summary>
    /// Background update task
    /// </summary>
    private async Task CivitModelQuery(CivitModelsRequest request, bool isInfiniteScroll = false)
    {
        var timer = Stopwatch.StartNew();
        var queryText = request.Query;
        var models = new List<CivitModel>();
        CivitModelsResponse? modelsResponse = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.CommaSeparatedModelIds))
            {
                // count IDs
                var ids = request.CommaSeparatedModelIds.Split(',');
                if (ids.Length > 100)
                {
                    var idChunks = ids.Chunk(100);
                    foreach (var chunk in idChunks)
                    {
                        request.CommaSeparatedModelIds = string.Join(",", chunk);
                        var chunkModelsResponse = await civitApi.GetModels(request);

                        if (chunkModelsResponse.Items != null)
                        {
                            models.AddRange(chunkModelsResponse.Items);
                        }
                    }
                }
                else
                {
                    modelsResponse = await civitApi.GetModels(request);
                    models = modelsResponse.Items;
                }
            }
            else
            {
                modelsResponse = await civitApi.GetModels(request);
                models = modelsResponse.Items;
            }

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

            var cacheNew = true;
            if (UseLocalCache)
            {
                // Database update calls will invoke `OnModelsUpdated`
                // Add to database
                await liteDbContext.UpsertCivitModelAsync(models);
                // Add as cache entry
                cacheNew = await liteDbContext.UpsertCivitModelQueryCacheEntryAsync(
                    new CivitModelQueryCacheEntry
                    {
                        Id = ObjectHash.GetMd5Guid(request),
                        InsertedAt = DateTimeOffset.UtcNow,
                        Request = request,
                        Items = models,
                        Metadata = modelsResponse?.Metadata,
                    }
                );
            }

            if (cacheNew)
            {
                var doesBaseModelTypeMatch =
                    SelectedBaseModels.Count == 0
                        ? request.BaseModels == null || request.BaseModels.Length == 0
                        : SelectedBaseModels.SequenceEqual(request.BaseModels);
                var doesModelTypeMatch =
                    SelectedModelType == CivitModelType.All
                        ? request.Types == null || request.Types.Length == 0
                        : SelectedModelType == request.Types?.FirstOrDefault();

                if (doesBaseModelTypeMatch && doesModelTypeMatch)
                {
                    UpdateModelCards(models, isInfiniteScroll);
                }
            }

            NextPageCursor = modelsResponse?.Metadata?.NextCursor;
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
            // Additional details
            var responseContent = e.Content ?? "[No Content]";
            var responseCode = e.StatusCode;
            var responseCodeName = e.StatusCode.ToString();

            Logger.Warn(
                e,
                "CivitAI query ApiException ({Request}), ({Code}: {Response})",
                request,
                responseCode,
                responseContent
            );

            notificationService.Show(
                new Notification(
                    "CivitAI can't be reached right now",
                    $"Please try again in a few minutes. ({responseCode}: {responseCodeName})",
                    NotificationType.Warning,
                    expiration: TimeSpan.Zero,
                    onClick: () =>
                        Dispatcher.UIThread.InvokeAsync(async () =>
                            await DialogHelper.CreateApiExceptionDialog(e).ShowAsync()
                        )
                )
            );
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
    private void UpdateModelCards(List<CivitModel>? models, bool addCards = false)
    {
        if (models is null)
        {
            modelCache.Clear();
            return;
        }

        var startIndex = modelCache.Count;

        var modelsToAdd = models.Select((m, i) => new OrderedValue<CivitModel>(startIndex + i, m));

        if (addCards)
        {
            var newModels = modelsToAdd.Where(x => !modelCache.Keys.Contains(x.Value.Id));
            modelCache.AddOrUpdate(newModels);
        }
        else
        {
            modelCache.EditDiff(modelsToAdd, static (a, b) => a.Order == b.Order && a.Value.Id == b.Value.Id);
        }

        // Status update
        ShowMainLoadingSpinner = false;
        IsIndeterminate = false;
        HasSearched = true;
    }

    private string previousSearchQuery = string.Empty;

    [RelayCommand]
    private async Task SearchModels(bool isInfiniteScroll = false)
    {
        var timer = Stopwatch.StartNew();

        if (SearchQuery != previousSearchQuery || !isInfiniteScroll)
        {
            // Reset page number
            previousSearchQuery = SearchQuery;
            NextPageCursor = null;
        }

        // Build request
        var modelRequest = new CivitModelsRequest
        {
            Nsfw = "true", // Handled by local view filter
            Sort = SortMode,
            Period = SelectedPeriod,
            Limit = 30,
        };

        if (NextPageCursor != null)
        {
            modelRequest.Cursor = NextPageCursor;
        }

        if (SelectedModelType != CivitModelType.All)
        {
            modelRequest.Types = [SelectedModelType];
        }

        if (SelectedBaseModels.Count > 0 && SelectedBaseModels.Count < AllBaseModels.Count)
        {
            modelRequest.BaseModels = SelectedBaseModels.ToArray();
        }

        if (SearchQuery.StartsWith("#"))
        {
            modelRequest.Tag = SearchQuery[1..];
        }
        else if (SearchQuery.StartsWith("@"))
        {
            modelRequest.Username = SearchQuery[1..];
        }
        else if (SearchQuery.StartsWith("$#"))
        {
            modelRequest.Period = CivitPeriod.AllTime;
            modelRequest.BaseModels = null;
            modelRequest.Types = null;
            modelRequest.CommaSeparatedModelIds = SearchQuery[2..];

            if (modelRequest.Sort is CivitSortMode.Favorites or CivitSortMode.Installed)
            {
                SortMode = CivitSortMode.HighestRated;
                modelRequest.Sort = CivitSortMode.HighestRated;
            }
        }
        else
        {
            modelRequest.Query = SearchQuery;
        }

        if (SortMode == CivitSortMode.Installed)
        {
            var connectedModels = await liteDbContext.LocalModelFiles.FindAsync(m =>
                m.ConnectedModelInfo != null
            );

            connectedModels = connectedModels.Where(x => x.HasCivitMetadata);

            modelRequest.CommaSeparatedModelIds = string.Join(
                ",",
                connectedModels
                    .Select(c => c.ConnectedModelInfo!.ModelId)
                    .GroupBy(m => m)
                    .Select(g => g.First())
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

        if (UseLocalCache)
        {
            cachedQuery = await liteDbContext.TryQueryWithClearOnExceptionAsync(
                liteDbContext.CivitModelQueryCache,
                liteDbContext
                    .CivitModelQueryCache.IncludeAll()
                    .FindByIdAsync(ObjectHash.GetMd5Guid(modelRequest))
            );
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
            NextPageCursor = cachedQuery.Metadata?.NextCursor;
            UpdateModelCards(cachedQuery.Items, isInfiniteScroll);

            // Start remote query (background mode)
            // Skip when last query was less than 2 min ago
            var timeSinceCache = DateTimeOffset.UtcNow - cachedQuery.InsertedAt;
            if (timeSinceCache?.TotalMinutes >= 2)
            {
                CivitModelQuery(modelRequest, isInfiniteScroll).SafeFireAndForget();
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
            await CivitModelQuery(modelRequest, isInfiniteScroll);
        }

        UpdateResultsText();
    }

    [RelayCommand]
    private void ClearOrSelectAllBaseModels()
    {
        if (SelectedBaseModels.Count == AllBaseModels.Count)
            AllBaseModels.ForEach(x => x.IsSelected = false);
        else
            AllBaseModels.ForEach(x => x.IsSelected = true);
    }

    public void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    public async Task LoadNextPageAsync()
    {
        if (NextPageCursor != null)
        {
            await SearchModelsCommand.ExecuteAsync(true);
        }
    }

    partial void OnSelectedPeriodChanged(CivitPeriod value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s =>
            s.ModelSearchOptions = new ModelSearchOptions(value, SortMode, SelectedModelType, string.Empty)
        );
        NextPageCursor = null;
    }

    partial void OnSortModeChanged(CivitSortMode value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s =>
            s.ModelSearchOptions = new ModelSearchOptions(
                SelectedPeriod,
                value,
                SelectedModelType,
                string.Empty
            )
        );
        NextPageCursor = null;
    }

    partial void OnSelectedModelTypeChanged(CivitModelType value)
    {
        TrySearchAgain().SafeFireAndForget();
        settingsManager.Transaction(s =>
            s.ModelSearchOptions = new ModelSearchOptions(SelectedPeriod, SortMode, value, string.Empty)
        );
        NextPageCursor = null;
    }

    private async Task TrySearchAgain(bool shouldUpdatePageNumber = true)
    {
        if (!HasSearched)
            return;

        modelCache.Clear();

        if (shouldUpdatePageNumber)
        {
            NextPageCursor = null;
        }

        // execute command instead of calling method directly so that the IsRunning property gets updated
        await SearchModelsCommand.ExecuteAsync(false);
    }

    private void UpdateResultsText()
    {
        NoResultsFound = ModelCards?.Count <= 0;
        NoResultsText = "No results found";
    }

    public override string Header => Resources.Label_CivitAi;
}
