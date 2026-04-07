using System.Collections.ObjectModel;
using System.ComponentModel;
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
    INavigationService<MainWindowViewModel> navigationService
) : TabViewModelBase, IInfinitelyScroll
{
    private bool suppressSearch;
    private bool filterOptionsLoaded;
    private int currentPage = 1;
    private int totalHits;

    [ObservableProperty]
    private ObservableCollection<CivArchiveSearchResult> results = [];

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> allModelTypes = [];

    [ObservableProperty]
    private ObservableCollection<BaseModelOptionViewModel> allBaseModels = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string tagQuery = string.Empty;

    [ObservableProperty]
    private string usernameQuery = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool noResultsFound;

    [ObservableProperty]
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
        await SearchModels();
    }

    [RelayCommand]
    private async Task SearchModels(bool isInfiniteScroll = false)
    {
        if (IsLoading)
        {
            return;
        }

        if (!isInfiniteScroll)
        {
            currentPage = 1;
            totalHits = 0;
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

            totalHits = response.TotalHits;
            currentPage = response.EffectiveFilters.Page;

            foreach (var item in response.Results)
            {
                if (isInfiniteScroll && Results.Any(existing => existing.Id == item.Id))
                {
                    continue;
                }

                Results.Add(item);
            }

            HasSearched = true;
            NoResultsFound = Results.Count == 0;
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
        foreach (var option in AllModelTypes)
        {
            option.IsSelected = shouldSelectAll;
        }
    }

    [RelayCommand]
    private void ToggleAllBaseModels()
    {
        var shouldSelectAll = AllBaseModels.Any(x => !x.IsSelected);
        foreach (var option in AllBaseModels)
        {
            option.IsSelected = shouldSelectAll;
        }
    }

    public async Task LoadNextPageAsync()
    {
        if (!IsLoading && Results.Count < totalHits)
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

        return new CivArchiveSearchFilters
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
            Types = selectedTypes.Count == AllModelTypes.Count ? [] : selectedTypes,
            BaseModels = selectedBaseModels.Count == AllBaseModels.Count ? [] : selectedBaseModels,
            Page = page,
        };
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
        if (e.PropertyName != nameof(BaseModelOptionViewModel.IsSelected) || suppressSearch)
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
