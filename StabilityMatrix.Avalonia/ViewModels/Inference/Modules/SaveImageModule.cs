using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

public class SaveImageModule : ModuleBase
{
    /// <inheritdoc />
    public SaveImageModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Save Intermediary Image";
        AddCards(vmFactory.Get<UpscalerCardViewModel>());
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        throw new System.NotImplementedException();
    }
}
