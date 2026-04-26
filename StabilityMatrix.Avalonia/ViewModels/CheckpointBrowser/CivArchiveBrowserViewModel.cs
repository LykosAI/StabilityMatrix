using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.CivArchive;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivArchiveBrowserPage))]
[RegisterSingleton<CivArchiveBrowserViewModel>]
public sealed partial class CivArchiveBrowserViewModel(
    ICivArchiveApiClient civArchiveApiClient,
    ISettingsManager settingsManager,
    IServiceManager<ViewModelBase> viewModelFactory,
    INavigationService<MainWindowViewModel> navigationService,
    IModelIndexService modelIndexService
) : TabViewModelBase, IInfinitelyScroll
{
    private bool suppressSearch;
    private bool filterOptionsLoaded;
    private bool searchQueued;
    private int currentPage = 1;

    /// <summary>
    /// All search results we've fetched so far across pages, regardless of client-side filters.
    /// Used as the source for <see cref="Results"/> rebuilds and dedupe checks.
    /// </summary>
    private readonly List<CivArchiveSearchResult> rawResults = [];

    [ObservableProperty]
    private ObservableCollection<CivArchiveSearchResult> results = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEndOfResults), nameof(HasResultCount))]
    private int totalHits;

    [ObservableProperty]
    private bool hideInstalledModels;

    [ObservableProperty]
    private double resizeFactor = 1.0;

    public bool IsEndOfResults => HasSearched && TotalHits > 0 && rawResults.Count >= TotalHits;

    public bool HasResultCount => HasSearched && TotalHits > 0;

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> allModelTypes = [];

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> allBaseModels = [];

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> filteredModelTypes = [];

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> filteredBaseModels = [];

    [ObservableProperty]
    private string modelTypeFilter = string.Empty;

    [ObservableProperty]
    private string baseModelFilter = string.Empty;

    public string ModelTypeSelectionSummary =>
        AllModelTypes.Count == 0
            ? string.Empty
            : $"{AllModelTypes.Count(x => x.IsSelected)} of {AllModelTypes.Count} selected";

    public string BaseModelSelectionSummary =>
        AllBaseModels.Count == 0
            ? string.Empty
            : $"{AllBaseModels.Count(x => x.IsSelected)} of {AllBaseModels.Count} selected";

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvancedFilters))]
    private string tagQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvancedFilters))]
    private string usernameQuery = string.Empty;

    /// <summary>
    /// True when the user has set explicit Tags or Username via the "More filters" flyout
    /// (not the inline @/# tokens — those are visible in the search box itself).
    /// Used to show a small dot indicator on the More Filters button.
    /// </summary>
    public bool HasAdvancedFilters =>
        !string.IsNullOrWhiteSpace(TagQuery) || !string.IsNullOrWhiteSpace(UsernameQuery);

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool noResultsFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEndOfResults), nameof(HasResultCount))]
    private bool hasSearched;

    [ObservableProperty]
    private string noResultsText = "No results found";

    [ObservableProperty]
    private NamedOption<CivArchivePlatformOption>? selectedPlatform;

    [ObservableProperty]
    private NamedOption<CivArchiveSortOption>? selectedSort;

    [ObservableProperty]
    private NamedOption<CivArchivePeriodOption>? selectedPeriod;

    [ObservableProperty]
    private NamedOption<CivArchiveRatingOption>? selectedRating;

    [ObservableProperty]
    private NamedOption<CivArchivePlatformStatusOption>? selectedPlatformStatus;

    [ObservableProperty]
    private NamedOption<CivArchiveKindOption>? selectedKind;

    public IReadOnlyList<NamedOption<CivArchivePlatformOption>> AllPlatforms { get; } =
        [
            new("All Platforms", CivArchivePlatformOption.All),
            new("CivitAI", CivArchivePlatformOption.Civitai),
            new("TensorArt", CivArchivePlatformOption.Tensorart),
            new("TensorHub", CivArchivePlatformOption.Tensorhub),
            new("SeaArt", CivArchivePlatformOption.Seaart),
            new("Civision", CivArchivePlatformOption.Civision),
            new("PixAI", CivArchivePlatformOption.Pixai),
            new("Tungsten", CivArchivePlatformOption.Tungsten),
            new("Yodayo", CivArchivePlatformOption.Yodayo),
            new("Moescape", CivArchivePlatformOption.Moescape),
            new("Shakker", CivArchivePlatformOption.Shakker),
            new("HuggingFace", CivArchivePlatformOption.Huggingface),
            new("ModelScope", CivArchivePlatformOption.Modelscope),
            new("ModelScope CN", CivArchivePlatformOption.ModelscopeCn),
        ];

    public IReadOnlyList<NamedOption<CivArchiveSortOption>> AllSorts { get; } =
        [
            new("Top", CivArchiveSortOption.Top),
            new("Newest", CivArchiveSortOption.Newest),
            new("Oldest", CivArchiveSortOption.Oldest),
            new("Relevance", CivArchiveSortOption.Relevance),
            new("Deleted Newest", CivArchiveSortOption.DeletedNewest),
            new("Deleted Oldest", CivArchiveSortOption.DeletedOldest),
        ];

    public IReadOnlyList<NamedOption<CivArchivePeriodOption>> AllPeriods { get; } =
        [
            new("All", CivArchivePeriodOption.All),
            new("Week", CivArchivePeriodOption.Week),
            new("Month", CivArchivePeriodOption.Month),
            new("Quarter", CivArchivePeriodOption.Quarter),
            new("Half", CivArchivePeriodOption.Half),
            new("Year", CivArchivePeriodOption.Year),
        ];

    public IReadOnlyList<NamedOption<CivArchiveRatingOption>> AllRatings { get; } =
        [
            new("Safe", CivArchiveRatingOption.Safe),
            new("All", CivArchiveRatingOption.All),
            new("Explicit", CivArchiveRatingOption.Explicit),
        ];

    public IReadOnlyList<NamedOption<CivArchivePlatformStatusOption>> AllPlatformStatuses { get; } =
        [
            new("All", CivArchivePlatformStatusOption.All),
            new("Available", CivArchivePlatformStatusOption.Available),
            new("Deleted", CivArchivePlatformStatusOption.Deleted),
        ];

    public IReadOnlyList<NamedOption<CivArchiveKindOption>> AllKinds { get; } =
        [
            new("All", CivArchiveKindOption.All),
            new("Version", CivArchiveKindOption.Version),
            new("User", CivArchiveKindOption.User),
            new("File", CivArchiveKindOption.File),
        ];

    public override string Header => "CivArchive";

    public override void OnLoaded()
    {
        if (!ViewModelState.HasFlag(ViewModelState.InitialLoaded))
        {
            RestoreSettings();
        }

        base.OnLoaded();
    }

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ResizeFactor,
                s => s.CivArchiveBrowserResizeFactor,
                true
            )
        );

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.HideInstalledModels,
                s => s.HideInstalledModelsInModelBrowser,
                true
            )
        );

        EventHandler indexHandler = (_, _) => Dispatcher.UIThread.Post(OnLocalModelIndexChanged);
        EventManager.Instance.ModelIndexChanged += indexHandler;
        AddDisposable(Disposable.Create(() => EventManager.Instance.ModelIndexChanged -= indexHandler));

        await SearchModels();
    }

    partial void OnHideInstalledModelsChanged(bool value) => RebuildVisibleResults();

    private void OnLocalModelIndexChanged()
    {
        // The local index changed (download finished or model deleted) — re-evaluate every
        // cached result's IsInstalled flag, then rebuild Results so the badge / hide-installed
        // filter reflect reality without needing a re-search.
        var hashes = modelIndexService.ModelIndexSha256Hashes;
        var urls = modelIndexService.ModelIndexCivArchiveUrls;
        foreach (var item in rawResults)
        {
            item.IsInstalled =
                (!string.IsNullOrEmpty(item.Url) && urls.Contains(item.Url))
                || (!string.IsNullOrEmpty(item.Sha256FromUrl) && hashes.Contains(item.Sha256FromUrl));
        }
        RebuildVisibleResults();
    }

    private void RebuildVisibleResults()
    {
        Results.Clear();
        foreach (var item in rawResults)
        {
            if (HideInstalledModels && item.IsInstalled)
                continue;
            Results.Add(item);
        }
        NoResultsFound = HasSearched && Results.Count == 0;
        OnPropertyChanged(nameof(IsEndOfResults));
    }

    [RelayCommand]
    private async Task SearchModels(bool isInfiniteScroll = false)
    {
        if (IsLoading)
        {
            if (!isInfiniteScroll)
            {
                searchQueued = true;
            }

            return;
        }

        if (!isInfiniteScroll)
        {
            searchQueued = false;
        }

        if (!isInfiniteScroll)
        {
            currentPage = 1;
            TotalHits = 0;
            rawResults.Clear();
            Results.Clear();
        }

        var filters = BuildFilters(isInfiniteScroll ? currentPage + 1 : currentPage);

        IsLoading = true;
        NoResultsFound = false;
        NoResultsText = "No results found";

        try
        {
            var response = await civArchiveApiClient.SearchAsync(filters);

            if (!filterOptionsLoaded)
            {
                ApplyFilterOptions(response.FilterOptions);
                filterOptionsLoaded = true;

                if (!isInfiniteScroll && suppressSearch)
                {
                    suppressSearch = false;
                    response = await civArchiveApiClient.SearchAsync(BuildFilters(currentPage));
                }
            }

            TotalHits = response.TotalHits;
            currentPage = response.EffectiveFilters.Page;

            var installedHashes = modelIndexService.ModelIndexSha256Hashes;
            var installedUrls = modelIndexService.ModelIndexCivArchiveUrls;
            foreach (var item in response.Results)
            {
                if (isInfiniteScroll && rawResults.Any(existing => existing.Id == item.Id))
                {
                    continue;
                }

                // URL match works for any CivArchive download with a stored SourceUrl;
                // SHA256 fallback covers the rare File-kind result with hash in URL.
                if (!string.IsNullOrEmpty(item.Url) && installedUrls.Contains(item.Url))
                {
                    item.IsInstalled = true;
                }
                else if (
                    !string.IsNullOrEmpty(item.Sha256FromUrl) && installedHashes.Contains(item.Sha256FromUrl)
                )
                {
                    item.IsInstalled = true;
                }

                rawResults.Add(item);
                if (!HideInstalledModels || !item.IsInstalled)
                {
                    Results.Add(item);
                }
            }

            HasSearched = true;
            NoResultsFound = Results.Count == 0;
            OnPropertyChanged(nameof(IsEndOfResults));
        }
        catch (Exception ex)
        {
            NoResultsFound = Results.Count == 0;
            NoResultsText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            SaveSettings();
        }

        if (searchQueued)
        {
            searchQueued = false;
            await SearchModels();
        }
    }

    [RelayCommand]
    private void ClearSearchQuery()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private async Task OpenResult(CivArchiveSearchResult? result)
    {
        if (result is null)
        {
            return;
        }

        switch (result.Kind)
        {
            case CivArchiveKindOption.User:
                await PivotToUser(result);
                break;
            case CivArchiveKindOption.File:
                ProcessRunner.OpenUrl(civArchiveApiClient.GetAbsoluteUri(result.Url).ToString());
                break;
            default:
                var detailsVm = viewModelFactory.Get<CivArchiveDetailsPageViewModel>(vm =>
                {
                    vm.RelativeUrl = result.Url;
                    return vm;
                });
                navigationService.NavigateTo(detailsVm, BetterSlideNavigationTransition.PageSlideFromRight);
                break;
        }
    }

    [RelayCommand]
    private void OpenOnCivArchive(CivArchiveSearchResult? result)
    {
        if (result is not null)
        {
            ProcessRunner.OpenUrl(civArchiveApiClient.GetAbsoluteUri(result.Url).ToString());
        }
    }

    [RelayCommand]
    private async Task SearchByCreator(CivArchiveSearchResult? result)
    {
        if (string.IsNullOrWhiteSpace(result?.Username))
        {
            return;
        }

        UsernameQuery = result.Username;
        await SearchModels();
    }

    [RelayCommand]
    private async Task CopySha256(CivArchiveSearchResult? result)
    {
        if (!string.IsNullOrWhiteSpace(result?.Sha256FromUrl) && App.Clipboard is not null)
        {
            await App.Clipboard.SetTextAsync(result.Sha256FromUrl);
        }
    }

    [RelayCommand]
    private void ToggleAllModelTypes()
    {
        var shouldSelectAll = AllModelTypes.Any(x => !x.IsSelected);
        suppressSearch = true;
        try
        {
            foreach (var option in AllModelTypes)
            {
                option.IsSelected = shouldSelectAll;
            }
        }
        finally
        {
            suppressSearch = false;
        }
        TriggerFilterSearch();
    }

    [RelayCommand]
    private void ToggleAllBaseModels()
    {
        var shouldSelectAll = AllBaseModels.Any(x => !x.IsSelected);
        suppressSearch = true;
        try
        {
            foreach (var option in AllBaseModels)
            {
                option.IsSelected = shouldSelectAll;
            }
        }
        finally
        {
            suppressSearch = false;
        }
        TriggerFilterSearch();
    }

    /// <summary>
    /// Reset every filter back to its default. Single property setter at the end re-triggers
    /// the search instead of one fetch per change.
    /// </summary>
    [RelayCommand]
    private async Task ResetFilters()
    {
        suppressSearch = true;
        try
        {
            SearchQuery = string.Empty;
            TagQuery = string.Empty;
            UsernameQuery = string.Empty;
            SelectedPlatform = AllPlatforms.First(x => x.Value == CivArchivePlatformOption.All);
            SelectedSort = AllSorts.First(x => x.Value == CivArchiveSortOption.Top);
            SelectedPeriod = AllPeriods.First(x => x.Value == CivArchivePeriodOption.All);
            SelectedRating = AllRatings.First(x => x.Value == CivArchiveRatingOption.Safe);
            SelectedPlatformStatus = AllPlatformStatuses.First(x =>
                x.Value == CivArchivePlatformStatusOption.All
            );
            SelectedKind = AllKinds.First(x => x.Value == CivArchiveKindOption.All);
            foreach (var option in AllModelTypes)
                option.IsSelected = true;
            foreach (var option in AllBaseModels)
                option.IsSelected = true;
        }
        finally
        {
            suppressSearch = false;
        }

        await SearchModels();
    }

    public async Task LoadNextPageAsync()
    {
        // Compare against rawResults so infinite-scroll keeps fetching even when
        // HideInstalledModels filters items out of Results.
        if (!IsLoading && rawResults.Count < TotalHits)
        {
            await SearchModels(true);
        }
    }

    private async Task PivotToUser(CivArchiveSearchResult result)
    {
        UsernameQuery = !string.IsNullOrWhiteSpace(result.Username) ? result.Username : result.Name;
        await SearchModels();
    }

    private void ApplyFilterOptions(CivArchiveFilterOptions options)
    {
        var savedOptions = settingsManager.Settings.CivArchiveBrowserOptions;

        suppressSearch = true;
        SetSelectableOptions(AllModelTypes, options.ModelTypes, savedOptions.SelectedModelTypes);
        SetSelectableOptions(AllBaseModels, options.BaseModels, savedOptions.SelectedBaseModels);
    }

    private void SetSelectableOptions(
        ObservableCollection<BaseModelOptionViewModel> target,
        IEnumerable<string> values,
        IReadOnlyCollection<string> selectedValues
    )
    {
        target.Clear();

        var sortedValues = values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var selectAll = selectedValues.Count == 0;

        foreach (var value in sortedValues)
        {
            var option = new BaseModelOptionViewModel
            {
                ModelType = value,
                IsSelected = selectAll || selectedValues.Contains(value),
            };
            option.PropertyChanged += OnSelectableOptionChanged;
            target.Add(option);
        }

        if (ReferenceEquals(target, AllModelTypes))
        {
            ApplyModelTypeFilter();
            OnPropertyChanged(nameof(ModelTypeSelectionSummary));
        }
        else if (ReferenceEquals(target, AllBaseModels))
        {
            ApplyBaseModelFilter();
            OnPropertyChanged(nameof(BaseModelSelectionSummary));
        }
    }

    partial void OnModelTypeFilterChanged(string value) => ApplyModelTypeFilter();

    partial void OnBaseModelFilterChanged(string value) => ApplyBaseModelFilter();

    private void ApplyModelTypeFilter() =>
        RefreshFilteredOptions(AllModelTypes, FilteredModelTypes, ModelTypeFilter);

    private void ApplyBaseModelFilter() =>
        RefreshFilteredOptions(AllBaseModels, FilteredBaseModels, BaseModelFilter);

    private static void RefreshFilteredOptions(
        ObservableCollection<BaseModelOptionViewModel> source,
        ObservableCollection<BaseModelOptionViewModel> target,
        string filter
    )
    {
        var query = filter?.Trim() ?? string.Empty;
        var matches = string.IsNullOrEmpty(query)
            ? source
            : source.Where(x => x.ModelType.Contains(query, StringComparison.OrdinalIgnoreCase));

        target.Clear();
        foreach (var item in matches)
        {
            target.Add(item);
        }
    }

    private void RestoreSettings()
    {
        var options = settingsManager.Settings.CivArchiveBrowserOptions;

        suppressSearch = true;
        SearchQuery = options.Query;
        TagQuery = options.Tags;
        UsernameQuery = options.Username;
        SelectedPlatform = AllPlatforms.First(x => x.Value == options.Platform);
        SelectedSort = AllSorts.First(x => x.Value == options.Sort);
        SelectedPeriod = AllPeriods.First(x => x.Value == options.Period);
        SelectedRating = AllRatings.First(x => x.Value == options.Rating);
        SelectedPlatformStatus = AllPlatformStatuses.First(x => x.Value == options.PlatformStatus);
        SelectedKind = AllKinds.First(x => x.Value == options.Kind);
        suppressSearch = false;
    }

    private CivArchiveSearchFilters BuildFilters(int page)
    {
        var selectedTypes = GetSelectedValues(AllModelTypes);
        var selectedBaseModels = GetSelectedValues(AllBaseModels);

        // Parse @user / #tag tokens inline from the search box and merge with
        // explicit values from the More Filters flyout. Inline tokens win for username
        // (only one allowed); tags are merged additively.
        var (cleanedQuery, parsedTags, parsedUsername) = ParseSearchQuery(SearchQuery);
        var combinedTags = string.Join(
            ",",
            new[] { TagQuery, parsedTags }.Where(s => !string.IsNullOrWhiteSpace(s))
        );
        var combinedUsername = !string.IsNullOrWhiteSpace(parsedUsername) ? parsedUsername : UsernameQuery;

        return new CivArchiveSearchFilters
        {
            Query = cleanedQuery,
            Tags = combinedTags,
            Username = combinedUsername,
            Platform = SelectedPlatform?.Value ?? CivArchivePlatformOption.All,
            Sort = SelectedSort?.Value ?? CivArchiveSortOption.Top,
            Period = SelectedPeriod?.Value ?? CivArchivePeriodOption.All,
            Rating = SelectedRating?.Value ?? CivArchiveRatingOption.Safe,
            PlatformStatus = SelectedPlatformStatus?.Value ?? CivArchivePlatformStatusOption.All,
            Kind = SelectedKind?.Value ?? CivArchiveKindOption.All,
            Types = selectedTypes.Count == AllModelTypes.Count ? [] : selectedTypes,
            BaseModels = selectedBaseModels.Count == AllBaseModels.Count ? [] : selectedBaseModels,
            Page = page,
        };
    }

    /// <summary>
    /// Pull <c>@user</c> and <c>#tag</c> tokens out of a free-form search string.
    /// Returns the leftover query (model name search), comma-joined tag list, and the
    /// parsed username (last-wins if multiple <c>@</c> tokens are present).
    /// </summary>
    internal static (string query, string tags, string username) ParseSearchQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (string.Empty, string.Empty, string.Empty);

        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var queryParts = new List<string>();
        var tags = new List<string>();
        string username = string.Empty;

        foreach (var token in tokens)
        {
            if (token.Length > 1 && token[0] == '@')
                username = token[1..]; // last @user wins
            else if (token.Length > 1 && token[0] == '#')
                tags.Add(token[1..]);
            else
                queryParts.Add(token);
        }

        return (string.Join(' ', queryParts), string.Join(',', tags), username);
    }

    private static List<string> GetSelectedValues(IEnumerable<BaseModelOptionViewModel> options)
    {
        return options.Where(x => x.IsSelected).Select(x => x.ModelType).ToList();
    }

    private void SaveSettings()
    {
        if (!settingsManager.IsLibraryDirSet)
        {
            return;
        }

        settingsManager.Transaction(s =>
            s.CivArchiveBrowserOptions = new CivArchiveBrowserOptions
            {
                Query = SearchQuery,
                Tags = TagQuery,
                Username = UsernameQuery,
                Platform = SelectedPlatform?.Value ?? CivArchivePlatformOption.All,
                Sort = SelectedSort?.Value ?? CivArchiveSortOption.Top,
                Period = SelectedPeriod?.Value ?? CivArchivePeriodOption.All,
                Rating = SelectedRating?.Value ?? CivArchiveRatingOption.Safe,
                PlatformStatus = SelectedPlatformStatus?.Value ?? CivArchivePlatformStatusOption.All,
                Kind = SelectedKind?.Value ?? CivArchiveKindOption.All,
                SelectedModelTypes = GetSelectedValues(AllModelTypes),
                SelectedBaseModels = GetSelectedValues(AllBaseModels),
            }
        );
    }

    private async void OnSelectableOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BaseModelOptionViewModel.IsSelected))
        {
            return;
        }

        OnPropertyChanged(nameof(ModelTypeSelectionSummary));
        OnPropertyChanged(nameof(BaseModelSelectionSummary));

        if (suppressSearch)
        {
            return;
        }

        SaveSettings();

        if (HasSearched)
        {
            await SearchModels();
        }
    }

    partial void OnSelectedPlatformChanged(NamedOption<CivArchivePlatformOption>? value) =>
        TriggerFilterSearch();

    partial void OnSelectedSortChanged(NamedOption<CivArchiveSortOption>? value) => TriggerFilterSearch();

    partial void OnSelectedPeriodChanged(NamedOption<CivArchivePeriodOption>? value) => TriggerFilterSearch();

    partial void OnSelectedRatingChanged(NamedOption<CivArchiveRatingOption>? value) => TriggerFilterSearch();

    partial void OnSelectedPlatformStatusChanged(NamedOption<CivArchivePlatformStatusOption>? value) =>
        TriggerFilterSearch();

    partial void OnSelectedKindChanged(NamedOption<CivArchiveKindOption>? value) => TriggerFilterSearch();

    private async void TriggerFilterSearch()
    {
        if (suppressSearch)
        {
            return;
        }

        SaveSettings();

        if (HasSearched)
        {
            await SearchModels();
        }
    }
}
