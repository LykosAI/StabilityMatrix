using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Refit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using Injectio.Attributes;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Resources = StabilityMatrix.Avalonia.Languages.Resources;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(OpenArtBrowserPage))]
[RegisterSingleton<OpenArtBrowserViewModel>]
public partial class OpenArtBrowserViewModel(
    IOpenArtApi openArtApi,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IPackageFactory packageFactory
) : TabViewModelBase, IInfinitelyScroll
{
    private const int PageSize = 20;

    public override string Header => Resources.Label_OpenArtBrowser;

    private readonly SourceCache<OpenArtSearchResult, string> searchResultsCache = new(x => x.Id);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCount), nameof(CanGoBack), nameof(CanGoForward), nameof(CanGoToEnd))]
    private OpenArtSearchResponse? latestSearchResponse;

    [ObservableProperty]
    private IObservableCollection<OpenArtSearchResult> searchResults =
        new ObservableCollectionExtended<OpenArtSearchResult>();

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InternalPageNumber), nameof(CanGoBack))]
    private int displayedPageNumber = 1;

    public int InternalPageNumber => DisplayedPageNumber - 1;

    public int PageCount =>
        Math.Max(
            1,
            Convert.ToInt32(Math.Ceiling((LatestSearchResponse?.Total ?? 0) / Convert.ToDouble(PageSize)))
        );

    public bool CanGoBack =>
        string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor) && InternalPageNumber > 0;

    public bool CanGoForward =>
        !string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor) || PageCount > InternalPageNumber + 1;

    public bool CanGoToEnd =>
        string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor) && PageCount > InternalPageNumber + 1;

    public IEnumerable<string> AllSortModes => ["Trending", "Latest", "Most Downloaded", "Most Liked"];

    [ObservableProperty]
    private string? selectedSortMode;

    protected override void OnInitialLoaded()
    {
        searchResultsCache
            .Connect()
            .DeferUntilLoaded()
            .Bind(SearchResults)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();
        SelectedSortMode = AllSortModes.First();
        DoSearch().SafeFireAndForget();
    }

    [RelayCommand]
    private async Task FirstPage()
    {
        DisplayedPageNumber = 1;
        searchResultsCache.Clear();

        await DoSearch();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        DisplayedPageNumber--;
        searchResultsCache.Clear();

        await DoSearch(InternalPageNumber);
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor))
        {
            DisplayedPageNumber++;
        }

        searchResultsCache.Clear();
        await DoSearch(InternalPageNumber);
    }

    [RelayCommand]
    private async Task LastPage()
    {
        if (string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor))
        {
            DisplayedPageNumber = PageCount;
        }

        searchResultsCache.Clear();
        await DoSearch(PageCount - 1);
    }

    [Localizable(false)]
    [RelayCommand]
    private void OpenModel(OpenArtSearchResult workflow)
    {
        ProcessRunner.OpenUrl($"https://openart.ai/workflows/{workflow.Id}");
    }

    [RelayCommand]
    private async Task SearchButton()
    {
        DisplayedPageNumber = 1;
        LatestSearchResponse = null;
        searchResultsCache.Clear();

        await DoSearch();
    }

    [RelayCommand]
    private async Task OpenWorkflow(OpenArtSearchResult workflow)
    {
        var vm = new OpenArtWorkflowViewModel(settingsManager, packageFactory) { Workflow = workflow };

        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = Resources.Action_Import,
            SecondaryButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsFooterVisible = true,
            MaxDialogWidth = 750,
            MaxDialogHeight = 850,
            CloseOnClickOutside = true,
            Content = vm
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        List<IPackageStep> steps =
        [
            new DownloadOpenArtWorkflowStep(openArtApi, vm.Workflow, settingsManager)
        ];

        // Add install steps if missing nodes and preferred
        if (
            vm is
            {
                InstallRequiredNodes: true,
                MissingNodes: { Count: > 0 } missingNodes,
                SelectedPackage: not null,
                SelectedPackagePair: not null
            }
        )
        {
            var extensionManager = vm.SelectedPackagePair.BasePackage.ExtensionManager!;

            steps.AddRange(
                missingNodes.Select(
                    extension =>
                        new InstallExtensionStep(
                            extensionManager,
                            vm.SelectedPackagePair.InstalledPackage,
                            extension
                        )
                )
            );
        }

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = Resources.Label_WorkflowImported,
            ModificationCompleteMessage = Resources.Label_FinishedImportingWorkflow
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        notificationService.Show(
            Resources.Label_WorkflowImported,
            Resources.Label_WorkflowImportComplete,
            NotificationType.Success
        );

        EventManager.Instance.OnWorkflowInstalled();
    }

    [RelayCommand]
    private void OpenOnOpenArt(OpenArtSearchResult? workflow)
    {
        if (workflow?.Id == null)
            return;

        ProcessRunner.OpenUrl($"https://openart.ai/workflows/{workflow.Id}");
    }

    private async Task DoSearch(int page = 0)
    {
        IsLoading = true;

        try
        {
            OpenArtSearchResponse? response = null;
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                var request = new OpenArtFeedRequest { Sort = GetSortMode(SelectedSortMode) };
                if (!string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor))
                {
                    request.Cursor = LatestSearchResponse.NextCursor;
                }

                response = await openArtApi.GetFeedAsync(request);
            }
            else
            {
                response = await openArtApi.SearchAsync(
                    new OpenArtSearchRequest
                    {
                        Keyword = SearchQuery,
                        PageSize = PageSize,
                        CurrentPage = page
                    }
                );
            }

            foreach (var item in response.Items)
            {
                searchResultsCache.AddOrUpdate(item);
            }

            LatestSearchResponse = response;
        }
        catch (ApiException e)
        {
            notificationService.Show(Resources.Label_ErrorRetrievingWorkflows, e.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSortModeChanged(string? value)
    {
        if (value is null || SearchResults.Count == 0)
            return;

        searchResultsCache.Clear();
        LatestSearchResponse = null;

        DoSearch().SafeFireAndForget();
    }

    public async Task LoadNextPageAsync()
    {
        if (!CanGoForward)
            return;

        try
        {
            OpenArtSearchResponse? response = null;
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                var request = new OpenArtFeedRequest { Sort = GetSortMode(SelectedSortMode) };
                if (!string.IsNullOrWhiteSpace(LatestSearchResponse?.NextCursor))
                {
                    request.Cursor = LatestSearchResponse.NextCursor;
                }

                response = await openArtApi.GetFeedAsync(request);
            }
            else
            {
                DisplayedPageNumber++;
                response = await openArtApi.SearchAsync(
                    new OpenArtSearchRequest
                    {
                        Keyword = SearchQuery,
                        PageSize = PageSize,
                        CurrentPage = InternalPageNumber
                    }
                );
            }

            foreach (var item in response.Items)
            {
                searchResultsCache.AddOrUpdate(item);
            }

            LatestSearchResponse = response;
        }
        catch (ApiException e)
        {
            notificationService.Show("Unable to load the next page", e.Message);
        }
    }

    private static string GetSortMode(string? sortMode)
    {
        return sortMode switch
        {
            "Trending" => "trending",
            "Latest" => "latest",
            "Most Downloaded" => "most_downloaded",
            "Most Liked" => "most_liked",
            _ => "trending"
        };
    }
}
