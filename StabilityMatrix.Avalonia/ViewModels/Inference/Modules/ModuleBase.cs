using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

public abstract class ModuleBase : StackExpanderViewModel, IComfyStep
{
    /// <inheritdoc />
    protected ModuleBase(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory) { }

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (
            (
                e.IsEnabledOverrides.TryGetValue(GetType(), out var isEnabledOverride)
                && !isEnabledOverride
            ) || !IsEnabled
        )
        {
            return;
        }

        OnApplyStep(e);
    }

    protected abstract void OnApplyStep(ModuleApplyStepEventArgs e);
}
