using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Avalonia.Services;

public class ServiceManager<T>
{
    // Holds providers
    private readonly Dictionary<Type, Func<T>> providers = new();
    
    // Holds singleton instances
    private readonly Dictionary<Type, T> instances = new();
    
    /// <summary>
    /// Register a new dialog view model (singleton instance)
    /// </summary>
    public ServiceManager<T> Register<TService>(TService instance) where TService : T
    {
        lock (instances)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}");
            }

            instances[typeof(TService)] = instance;
        }
        
        return this;
    }
    
    /// <summary>
    /// Register a new dialog view model provider action (called on each dialog creation)
    /// </summary>
    public ServiceManager<T> Register<TService>(Func<TService> provider) where TService : T
    {
        lock (providers)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}");
            }

            // Return type is wrong during build with method group syntax
            // ReSharper disable once RedundantCast
            providers[typeof(TService)] = () => (TService) provider();
        }

        return this;
    }
    
    /// <summary>
    /// Register a new dialog view model instance using a service provider
    /// Equal to Register[TService](serviceProvider.GetRequiredService[TService])
    /// </summary>
    public ServiceManager<T> RegisterProvider<TService>(IServiceProvider provider) where TService : notnull, T
    {
        lock (providers)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}");
            }
            
            // Return type is wrong during build with method group syntax
            // ReSharper disable once RedundantCast
            providers[typeof(TService)] = () => (TService) provider.GetRequiredService<TService>();
        }

        return this;
    }
    
    /// <summary>
    /// Get a view model instance
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public TService Get<TService>() where TService : T
    {
        if (instances.TryGetValue(typeof(TService), out var instance))
        {
            if (instance is null)
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} was registered as null");
            }
            return (TService) instance;
        }

        if (providers.TryGetValue(typeof(TService), out var provider))
        {
            if (provider is null)
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} was registered as null");
            }
            var result = provider();
            if (result is null)
            {
                throw new ArgumentException(
                    $"Service provider for type {typeof(TService)} returned null");
            }
            return (TService) result;
        }

        throw new ArgumentException(
            $"Service of type {typeof(TService)} is not registered for {typeof(T)}");
    }

}
