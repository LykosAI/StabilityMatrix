namespace StabilityMatrix.Avalonia.Models.Inference;

public interface IComfyStep
{
    void ApplyStep(ModuleApplyStepEventArgs e);
}
