using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public interface IComfyStep
{
    void ApplyStep(ModuleApplyStepEventArgs e);
}
