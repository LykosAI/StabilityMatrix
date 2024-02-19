using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Refit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(OpenArtBrowserPage))]
[Singleton]
public partial class OpenArtBrowserViewModel(
    IOpenArtApi openArtApi,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IPackageFactory packageFactory
) : PageViewModelBase, IInfinitelyScroll
{
    private const int PageSize = 20;

    public override string Title => Resources.Label_Workflows;
    public override IconSource IconSource => new FASymbolIconSource { Symbol = "fa-solid fa-circle-nodes" };

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

    public bool CanGoBack => InternalPageNumber > 0;

    public bool CanGoForward => PageCount > InternalPageNumber + 1;

    public bool CanGoToEnd => PageCount > InternalPageNumber + 1;

    protected override void OnInitialLoaded()
    {
        searchResultsCache.Connect().DeferUntilLoaded().Bind(SearchResults).Subscribe();
    }

    public override async Task OnLoadedAsync()
    {
        if (SearchResults.Any())
            return;

        await DoSearch();
    }

    [RelayCommand]
    private async Task FirstPage()
    {
        DisplayedPageNumber = 1;
        await DoSearch();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        DisplayedPageNumber--;
        await DoSearch(InternalPageNumber);
    }

    [RelayCommand]
    private async Task NextPage()
    {
        DisplayedPageNumber++;
        await DoSearch(InternalPageNumber);
    }

    [RelayCommand]
    private async Task LastPage()
    {
        DisplayedPageNumber = PageCount;
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

        var steps = new List<IPackageStep>();
        foreach (var node in vm.CustomNodes.Where(x => x.IsInstalled is false))
        {
            if (ComfyNodeMap.Lookup.TryGetValue(node.Title, out var url))
            {
                steps.Add(
                    new InstallExtensionStep(
                        comfyPair.BasePackage.ExtensionManager,
                        comfyPair.InstalledPackage,
                        new PackageExtension
                        {
                            Title = node.Title,
                            Reference = new Uri(url),
                            Files = [new Uri(url)],
                            InstallType = "git-clone",
                            Author = node.Title
                        }
                    )
                );
            }
        }

        steps.Add(new DownloadOpenArtWorkflowStep(openArtApi, vm.Workflow, settingsManager));

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Workflow Imported",
            ModificationCompleteMessage = "Finished importing workflow and custom nodes"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);
    }

    private async Task DoSearch(int page = 0)
    {
        IsLoading = true;

        try
        {
            var response = await openArtApi.SearchAsync(
                new OpenArtSearchRequest
                {
                    Keyword = SearchQuery,
                    PageSize = PageSize,
                    CurrentPage = page
                }
            );

            searchResultsCache.EditDiff(response.Items, (a, b) => a.Id == b.Id);
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

    public async Task LoadNextPageAsync()
    {
        if (!CanGoForward)
            return;

        try
        {
            DisplayedPageNumber++;
            var response = await openArtApi.SearchAsync(
                new OpenArtSearchRequest
                {
                    Keyword = SearchQuery,
                    PageSize = PageSize,
                    CurrentPage = InternalPageNumber
                }
            );

            searchResultsCache.AddOrUpdate(response.Items);
            LatestSearchResponse = response;
        }
        catch (ApiException e)
        {
            notificationService.Show("Unable to load the next page", e.Message);
        }
    }
}
