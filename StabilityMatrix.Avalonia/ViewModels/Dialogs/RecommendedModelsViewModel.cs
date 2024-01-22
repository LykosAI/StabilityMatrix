using System.Threading.Tasks;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public class RecommendedModelsViewModel(ILykosAuthApi lykosApi, ICivitApi civitApi)
    : ContentDialogViewModelBase
{
    public override async Task OnLoadedAsync()
    {
        var recommendedModels = await lykosApi.GetRecommendedModels();
    }
}
