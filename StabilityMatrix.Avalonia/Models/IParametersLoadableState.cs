using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models;

public interface IParametersLoadableState
{
    void LoadStateFromParameters(GenerationParameters parameters);

    GenerationParameters SaveStateToParameters(GenerationParameters parameters);

    public GenerationParameters SaveStateToParameters()
    {
        return SaveStateToParameters(new GenerationParameters());
    }
}
