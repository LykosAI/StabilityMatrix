using System.ComponentModel;

namespace StabilityMatrix.Avalonia.Services;

[Localizable(false)]
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
    /// Register a new service provider action with Scoped lifetime.
    /// The factory is called once per scope.
    /// </summary>
    IServiceManager<T> RegisterScoped<TService>(Func<IServiceProvider, TService> provider)
        where TService : T;

    /// <summary>
    /// Creates a new service scope.
    /// </summary>
    /// <returns>An IServiceManagerScope representing the created scope.</returns>
    IServiceManagerScope<T> CreateScope();

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
    /// Register a new service provider action with Scoped lifetime.
    /// The factory is called once per scope.
    /// </summary>
    IServiceManager<T> RegisterScoped(Type type, Func<IServiceProvider, T> provider);
}
