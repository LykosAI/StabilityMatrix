using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Avalonia.Services;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[Localizable(false)]
public class ServiceManager<T> : IServiceManager<T>
{
    // Holds providers
    private readonly Dictionary<Type, Func<T>> providers = new();

    // Holds singleton instances
    private readonly Dictionary<Type, T> instances = new();

    /// <summary>
    /// Register a new dialog view model (singleton instance)
    /// </summary>
    public IServiceManager<T> Register<TService>(TService instance)
        where TService : T
    {
        if (instance is null)
            throw new ArgumentNullException(nameof(instance));

        lock (instances)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}"
                );
            }

            instances[instance.GetType()] = instance;
        }

        return this;
    }

    /// <summary>
    /// Register a new dialog view model provider action (called on each dialog creation)
    /// </summary>
    public IServiceManager<T> Register<TService>(Func<TService> provider)
        where TService : T
    {
        lock (providers)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}"
                );
            }

            // Return type is wrong during build with method group syntax
            // ReSharper disable once RedundantCast
            providers[typeof(TService)] = () => (TService)provider();
        }

        return this;
    }

    /// <summary>
    /// Register a new dialog view model instance using a service provider
    /// Equal to Register[TService](serviceProvider.GetRequiredService[TService])
    /// </summary>
    public IServiceManager<T> RegisterProvider<TService>(IServiceProvider provider)
        where TService : notnull, T
    {
        lock (providers)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}"
                );
            }

            // Return type is wrong during build with method group syntax
            // ReSharper disable once RedundantCast
            providers[typeof(TService)] = () => (TService)provider.GetRequiredService<TService>();
        }

        return this;
    }

    /// <summary>
    /// Get a view model instance from runtime type
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public T Get(Type serviceType)
    {
        if (!serviceType.IsAssignableTo(typeof(T)))
        {
            throw new ArgumentException($"Service type {serviceType} is not assignable to {typeof(T)}");
        }

        if (instances.TryGetValue(serviceType, out var instance))
        {
            if (instance is null)
            {
                throw new ArgumentException($"Service of type {serviceType} was registered as null");
            }
            return (T)instance;
        }

        if (providers.TryGetValue(serviceType, out var provider))
        {
            if (provider is null)
            {
                throw new ArgumentException($"Service of type {serviceType} was registered as null");
            }
            var result = provider();
            if (result is null)
            {
                throw new ArgumentException($"Service provider for type {serviceType} returned null");
            }
            return (T)result;
        }

        throw new ArgumentException($"Service of type {serviceType} is not registered for {typeof(T)}");
    }

    /// <summary>
    /// Get a view model instance
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public TService Get<TService>()
        where TService : T
    {
        if (instances.TryGetValue(typeof(TService), out var instance))
        {
            if (instance is null)
            {
                throw new ArgumentException($"Service of type {typeof(TService)} was registered as null");
            }
            return (TService)instance;
        }

        if (providers.TryGetValue(typeof(TService), out var provider))
        {
            if (provider is null)
            {
                throw new ArgumentException($"Service of type {typeof(TService)} was registered as null");
            }
            var result = provider();
            if (result is null)
            {
                throw new ArgumentException($"Service provider for type {typeof(TService)} returned null");
            }
            return (TService)result;
        }

        throw new ArgumentException($"Service of type {typeof(TService)} is not registered for {typeof(T)}");
    }

    /// <summary>
    /// Get a view model instance with an initializer parameter
    /// </summary>
    public TService Get<TService>(Func<TService, TService> initializer)
        where TService : T
    {
        var instance = Get<TService>();
        return initializer(instance);
    }

    /// <summary>
    /// Get a view model instance with an initializer for a mutable instance
    /// </summary>
    public TService Get<TService>(Action<TService> initializer)
        where TService : T
    {
        var instance = Get<TService>();
        initializer(instance);
        return instance;
    }

    public void Register(Type type, Func<T> providerFunc)
    {
        lock (providers)
        {
            if (instances.ContainsKey(type) || providers.ContainsKey(type))
            {
                throw new ArgumentException($"Service of type {type} is already registered for {typeof(T)}");
            }

            providers[type] = providerFunc;
        }
    }
}
