using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Services;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
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
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        
        lock (instances)
        {
            if (instances.ContainsKey(typeof(TService)) || providers.ContainsKey(typeof(TService)))
            {
                throw new ArgumentException(
                    $"Service of type {typeof(TService)} is already registered for {typeof(T)}");
            }

            instances[instance.GetType()] = instance;
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
    /// Get a service instance
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public T Get(Type type)
    {
        if (instances.TryGetValue(type, out var instance))
        {
            if (instance is null)
            {
                throw new ArgumentException(
                    $"Service of type {type} was registered as null");
            }
            return instance;
        }

        if (providers.TryGetValue(type, out var provider))
        {
            if (provider is null)
            {
                throw new ArgumentException(
                    $"Service of type {type} was registered as null");
            }
            var result = provider();
            if (result is null)
            {
                throw new ArgumentException(
                    $"Service provider for type {type} returned null");
            }
            return result;
        }

        throw new ArgumentException(
            $"Service of type {type} is not registered in ServiceManager for {typeof(T)}");
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
    
    /// <summary>
    /// Get a view model instance from runtime type
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public T Get(Type serviceType)
    {
        if (!serviceType.IsAssignableFrom(typeof(T)))
        {
            throw new ArgumentException(
                $"Service type {serviceType} is not assignable from {typeof(T)}");
        }
        
        if (instances.TryGetValue(serviceType, out var instance))
        {
            if (instance is null)
            {
                throw new ArgumentException(
                    $"Service of type {serviceType} was registered as null");
            }
            return (T) instance;
        }

        if (providers.TryGetValue(serviceType, out var provider))
        {
            if (provider is null)
            {
                throw new ArgumentException(
                    $"Service of type {serviceType} was registered as null");
            }
            var result = provider();
            if (result is null)
            {
                throw new ArgumentException(
                    $"Service provider for type {serviceType} returned null");
            }
            return (T) result;
        }

        throw new ArgumentException(
            $"Service of type {serviceType} is not registered for {typeof(T)}");
    }
    
    /// <summary>
    /// Get a view model instance with an initializer parameter
    /// </summary>
    public TService Get<TService>(Func<TService, TService> initializer) where TService : T
    {
        var instance = Get<TService>();
        return initializer(instance);
    }
    
    /// <summary>
    /// Get a view model instance with an initializer for a mutable instance
    /// </summary>
    public TService Get<TService>(Action<TService> initializer) where TService : T
    {
        var instance = Get<TService>();
        initializer(instance);
        return instance;
    }
    
    /// <summary>
    /// Get a view model instance, set as DataContext of its View, and return
    /// a BetterContentDialog with that View as its Content
    /// </summary>
    public BetterContentDialog GetDialog<TService>() where TService : T
    {
        var instance = Get<TService>()!;
        
        if (Attribute.GetCustomAttribute(instance.GetType(), typeof(ViewAttribute)) is not ViewAttribute
            viewAttr)
        {
            throw new InvalidOperationException($"View not found for {instance.GetType().FullName}");
        }

        if (Activator.CreateInstance(viewAttr.GetViewType()) is not Control view)
        {
            throw new NullReferenceException($"Unable to create instance for {instance.GetType().FullName}");
        }
        
        return new BetterContentDialog { Content = view };
    }
    
    /// <summary>
    /// Get a view model instance with initializer, set as DataContext of its View, and return
    /// a BetterContentDialog with that View as its Content
    /// </summary>
    public BetterContentDialog GetDialog<TService>(Action<TService> initializer) where TService : T
    {
        var instance = Get(initializer)!;
        
        if (Attribute.GetCustomAttribute(instance.GetType(), typeof(ViewAttribute)) is not ViewAttribute
            viewAttr)
        {
            throw new InvalidOperationException($"View not found for {instance.GetType().FullName}");
        }

        if (Activator.CreateInstance(viewAttr.GetViewType()) is not Control view)
        {
            throw new NullReferenceException($"Unable to create instance for {instance.GetType().FullName}");
        }
        
        view.DataContext = instance;
        
        return new BetterContentDialog { Content = view };
    }
}
