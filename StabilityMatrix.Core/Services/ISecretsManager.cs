using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Interface for managing secure settings and tokens.
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// Load and return the secrets.
    /// </summary>
    Task<Secrets> LoadAsync();

    /// <summary>
    /// Load and return the secrets, or save and return a new instance on error.
    /// </summary>
    Task<Secrets> SafeLoadAsync();

    Task SaveAsync(Secrets secrets);
}
