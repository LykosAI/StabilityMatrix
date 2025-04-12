namespace StabilityMatrix.Avalonia.Services;

public interface IServiceManager<T>
{
    /// <summary>
    /// Register a new dialog view model (singleton instance)
    /// </summary>
    IServiceManager<T> Register<TService>(TService instance)
        where TService : T;

    /// <summary>
    /// Register a new dialog view model provider action (called on each dialog creation)
    /// </summary>
    IServiceManager<T> Register<TService>(Func<TService> provider)
        where TService : T;

    void Register(Type type, Func<T> providerFunc);

    /// <summary>
    /// Register a new dialog view model instance using a service provider
    /// Equal to Register[TService](serviceProvider.GetRequiredService[TService])
    /// </summary>
    IServiceManager<T> RegisterProvider<TService>(IServiceProvider provider)
        where TService : notnull, T;

    /// <summary>
    /// Get a view model instance from runtime type
    /// </summary>
    T Get(Type serviceType);

    /// <summary>
    /// Get a view model instance
    /// </summary>
    TService Get<TService>()
        where TService : T;

    /// <summary>
    /// Get a view model instance with an initializer parameter
    /// </summary>
    TService Get<TService>(Func<TService, TService> initializer)
        where TService : T;

    /// <summary>
    /// Get a view model instance with an initializer for a mutable instance
    /// </summary>
    TService Get<TService>(Action<TService> initializer)
        where TService : T;
}
