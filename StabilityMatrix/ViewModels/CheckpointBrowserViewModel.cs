using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Refit;
using StabilityMatrix.Api;
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
    private const int MaxModelsPerPage = 14;

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

    public CheckpointBrowserViewModel(ICivitApi civitApi, IDownloadService downloadService, ISnackbarService snackbarService)
    {
        this.civitApi = civitApi;
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;
        
        SelectedPeriod = CivitPeriod.Month;
        SortMode = CivitSortMode.HighestRated;
        SelectedModelType = CivitModelType.All;
        HasSearched = false;
        CurrentPageNumber = 1;
        CanGoToPreviousPage = false;
        CanGoToNextPage = true;
    }

    [RelayCommand]
    private async Task SearchModels()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }
        
        ShowMainLoadingSpinner = true;

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

        try
        {
            var models = await civitApi.GetModels(modelRequest);

            HasSearched = true;
            TotalPages = models.Metadata.TotalPages;
            CanGoToPreviousPage = CurrentPageNumber > 1;
            CanGoToNextPage = CurrentPageNumber < TotalPages;
            ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>(models.Items.Select(
                m => new CheckpointBrowserCardViewModel(m, downloadService, snackbarService)));
            Logger.Debug($"Found {models.Items.Length} models");
        }
        catch (ApiException e)
        {
            snackbarService.ShowSnackbarAsync("The service may be unavailable. Please try again later.",
                "Data could not be retrieved").SafeFireAndForget();
            Logger.Log(NLog.LogLevel.Error, e);
        }

        ShowMainLoadingSpinner = false;

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
