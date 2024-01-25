using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
public class RecommendedModelsViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<RecommendedModelsViewModel> logger;
    private readonly ILykosAuthApi lykosApi;
    private readonly ICivitApi civitApi;
    private readonly ILiteDbContext liteDbContext;
    public SourceCache<RecommendedModelItemViewModel, int> CivitModels { get; } = new(p => p.ModelVersion.Id);

    public IObservableCollection<RecommendedModelItemViewModel> Sd15Models { get; set; } =
        new ObservableCollectionExtended<RecommendedModelItemViewModel>();

    public IObservableCollection<RecommendedModelItemViewModel> SdxlModels { get; } =
        new ObservableCollectionExtended<RecommendedModelItemViewModel>();

    public RecommendedModelsViewModel(
        ILogger<RecommendedModelsViewModel> logger,
        ILykosAuthApi lykosApi,
        ICivitApi civitApi,
        ILiteDbContext liteDbContext
    )
    {
        this.logger = logger;
        this.lykosApi = lykosApi;
        this.civitApi = civitApi;
        this.liteDbContext = liteDbContext;

        CivitModels
            .Connect()
            .DeferUntilLoaded()
            .Filter(f => f.ModelVersion.BaseModel == "SD 1.5")
            .Bind(Sd15Models)
            .Subscribe();

        CivitModels
            .Connect()
            .DeferUntilLoaded()
            .Filter(f => f.ModelVersion.BaseModel == "SDXL 1.0")
            .Bind(SdxlModels)
            .Subscribe();
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        var recommendedModels = await lykosApi.GetRecommendedModels();
    }
}
