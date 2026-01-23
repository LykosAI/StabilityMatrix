namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Registry for local provider model managers.
/// Returns the appropriate model manager for a given provider ID.
/// </summary>
public static class LocalProviderModelManagerRegistry
{
    private static readonly Dictionary<string, ILocalProviderModelManager> Managers = new(
        StringComparer.OrdinalIgnoreCase
    );

    static LocalProviderModelManagerRegistry()
    {
        // Register all local provider model managers
        Register(new FluxKontextModelManager());
        Register(new QwenImageEditModelManager());
    }

    /// <summary>
    /// Register a model manager
    /// </summary>
    public static void Register(ILocalProviderModelManager manager)
    {
        Managers[manager.ProviderId] = manager;
    }

    /// <summary>
    /// Get the model manager for a provider ID, or null if not found
    /// </summary>
    public static ILocalProviderModelManager? GetManager(string? providerId)
    {
        return string.IsNullOrEmpty(providerId) ? null : Managers.GetValueOrDefault(providerId);
    }

    /// <summary>
    /// Check if a provider has a registered model manager
    /// </summary>
    public static bool HasManager(string? providerId)
    {
        return !string.IsNullOrEmpty(providerId) && Managers.ContainsKey(providerId);
    }

    /// <summary>
    /// Get all registered provider IDs
    /// </summary>
    public static IEnumerable<string> GetRegisteredProviderIds() => Managers.Keys;
}
