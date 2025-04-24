using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apizr;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Fusillade;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(OpenModelDbBrowserPage))]
[RegisterSingleton<OpenModelDbBrowserViewModel>]
public sealed partial class OpenModelDbBrowserViewModel(
    ILogger<OpenModelDbBrowserViewModel> logger,
    IServiceManager<ViewModelBase> vmManager,
    OpenModelDbManager openModelDbManager,
    INotificationService notificationService
) : TabViewModelBase
{
    // ReSharper disable once LocalizableElement
    public override string Header => "OpenModelDB";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string selectedSortOption = "Latest";

    public SourceCache<OpenModelDbKeyedModel, string> ModelCache { get; } = new(static x => x.Id);

    public IObservableCollection<OpenModelDbBrowserCardViewModel> FilteredModelCards { get; } =
        new ObservableCollectionExtended<OpenModelDbBrowserCardViewModel>();

    public List<string> SortOptions =>
        ["Latest", "Largest Scale", "Smallest Scale", "Largest Size", "Smallest Size"];

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();

        ModelCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(SearchQueryPredicate)
            .Transform(model => new OpenModelDbBrowserCardViewModel(openModelDbManager) { Model = model })
            .SortAndBind(FilteredModelCards, SortComparer)
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            await LoadDataAsync();
        }
        catch (ApizrException<OpenModelDbModelsResponse> e)
        {
            logger.LogWarning(e, "Failed to load models from OpenModelDB");
            notificationService.ShowPersistent("Failed to load models from OpenModelDB", e.Message);
        }

        SearchQueryReload.OnNext(Unit.Default);
    }

    [RelayCommand]
    private async Task OpenModelCardAsync(OpenModelDbBrowserCardViewModel? card)
    {
        if (card?.Model is not { } model)
        {
            return;
        }

        var vm = vmManager.Get<OpenModelDbModelDetailsViewModel>();
        vm.Model = model;

        var dialog = vm.GetDialog();
        dialog.MaxDialogHeight = 920;
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Populate the model cache from api.
    /// </summary>
    private async Task LoadDataAsync(Priority priority = default)
    {
        await openModelDbManager.EnsureMetadataLoadedAsync();

        var response = await openModelDbManager.ExecuteAsync(
            api => api.GetModels(),
            options => options.WithPriority(priority)
        );

        if (ModelCache.Count == 0)
        {
            ModelCache.Edit(innerCache =>
            {
                innerCache.Load(response.GetKeyedModels());
            });
        }
        else
        {
            ModelCache.EditDiff(response.GetKeyedModels());
        }
    }
}
