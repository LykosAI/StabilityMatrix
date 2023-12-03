using System.Collections.Generic;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

public abstract class ModuleBase : StackExpanderViewModel, IComfyStep, IInputImageProvider
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

    /// <inheritdoc />
    IEnumerable<ImageSource> IInputImageProvider.GetInputImages() => GetInputImages();

    protected virtual IEnumerable<ImageSource> GetInputImages()
    {
        yield break;
    }
}
