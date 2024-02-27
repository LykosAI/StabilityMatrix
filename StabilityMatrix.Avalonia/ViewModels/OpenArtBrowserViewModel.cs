using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
[Singleton]
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
        searchResultsCache.Connect().DeferUntilLoaded().Bind(SearchResults).Subscribe();
        SelectedSortMode = AllSortModes.First();
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
        var existingComfy = settingsManager.Settings.InstalledPackages.FirstOrDefault(
            x => x.PackageName is "ComfyUI"
        );

        var comfyPair = packageFactory.GetPackagePair(existingComfy);

        var vm = new OpenArtWorkflowViewModel { Workflow = workflow, InstalledComfy = comfyPair };

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

        if (existingComfy == null || comfyPair == null)
        {
            notificationService.Show(
                Resources.Label_ComfyRequiredTitle,
                "ComfyUI is required to import workflows from OpenArt"
            );
            return;
        }

        if (vm.MissingNodes is not { Count: > 0 } missingNodes)
        {
            // Skip if no missing nodes
            return;
        }

        var extensionManager = comfyPair.BasePackage.ExtensionManager!;

        List<IPackageStep> steps =
        [
            new DownloadOpenArtWorkflowStep(openArtApi, vm.Workflow, settingsManager),
            ..missingNodes.Select(
                extension => new InstallExtensionStep(extensionManager, comfyPair.InstalledPackage, extension)
            )
        ];

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Workflow Imported",
            ModificationCompleteMessage = "Finished importing workflow and custom nodes"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        notificationService.Show(
            "Workflow Imported",
            "The workflow and custom nodes have been imported.",
            NotificationType.Success
        );
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
            notificationService.Show("Error retrieving workflows", e.Message);
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
