namespace StabilityMatrix.Avalonia.Services;

public interface IConnectedServiceManager
{
    /// <summary>
    /// Attempt to enable CivitUseDiscoveryApi, prompting for login as needed.
    /// </summary>
    Task<bool> PromptEnableCivitUseDiscoveryApi();

    /// <summary>
    /// Prompts the user to log in to their Lykos account if they are not already logged in.
    /// </summary>
    /// <returns>True if the user is logged in after this function, false otherwise.</returns>
    Task<bool> PromptLoginIfRequired();
}
