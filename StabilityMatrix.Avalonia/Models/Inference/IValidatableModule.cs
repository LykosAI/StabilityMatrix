using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Models.Inference;

public interface IValidatableModule
{
    public Task<bool> Validate();
}
