using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Apizr;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(OpenModelDbBrowserPage))]
[Singleton]
public partial class OpenModelDbBrowserViewModel(
    ILogger<OpenModelDbBrowserViewModel> logger,
    IApizrManager<IOpenModelDbApi> apiManager,
    INotificationService notificationService
) : TabViewModelBase
{
    // ReSharper disable once LocalizableElement
    public override string Header => "OpenModelDB";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? searchQuery;

    private readonly SourceCache<OpenModelDbKeyedModel, string> modelCache = new(static x => x.Id);

    public IObservableCollection<OpenModelDbBrowserCardViewModel> FilteredModelCards { get; } =
        new ObservableCollectionExtended<OpenModelDbBrowserCardViewModel>();

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();

        modelCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(SearchQueryPredicate)
            .Transform(model => new OpenModelDbBrowserCardViewModel { Model = model })
            .SortAndBind(
                FilteredModelCards,
                SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Descending(
                    card => card.Model?.Date ?? DateOnly.MinValue
                )
            )
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
    }

    /// <summary>
    /// Populate the model cache from api.
    /// </summary>
    private async Task LoadDataAsync()
    {
        var response = await apiManager.ExecuteAsync(api => api.GetModels());

        modelCache.Edit(innerCache =>
        {
            innerCache.Load(response.GetKeyedModels());
        });
    }
}
