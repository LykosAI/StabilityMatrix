using System.ComponentModel;
using Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Extensions;

[Localizable(false)]
public static class ServiceManagerExtensions
{
    /// <summary>
    /// Get a view model instance with an initializer parameter
    /// </summary>
    public static TService Get<TService>(
        this IServiceManager<TService> serviceManager,
        Func<TService, TService> initializer
    )
    {
        var instance = serviceManager.Get<TService>();
        return initializer(instance);
    }

    /// <summary>
    /// Get a view model instance with an initializer for a mutable instance
    /// </summary>
    public static TService Get<TService>(
        this IServiceManager<TService> serviceManager,
        Action<TService> initializer
    )
    {
        var instance = serviceManager.Get<TService>();
        initializer(instance);
        return instance;
    }

    /// <summary>
    /// Get a view model instance, set as DataContext of its View, and return
    /// a BetterContentDialog with that View as its Content
    /// </summary>
    public static BetterContentDialog GetDialog<TService>(this IServiceManager<TService> serviceManager)
    {
        var instance = serviceManager.Get<TService>()!;

        if (
            Attribute.GetCustomAttribute(instance.GetType(), typeof(ViewAttribute))
            is not ViewAttribute viewAttr
        )
        {
            throw new InvalidOperationException($"View not found for {instance.GetType().FullName}");
        }

        if (Activator.CreateInstance(viewAttr.ViewType) is not Control view)
        {
            throw new NullReferenceException($"Unable to create instance for {instance.GetType().FullName}");
        }

        return new BetterContentDialog { Content = view };
    }

    /// <summary>
    /// Get a view model instance with initializer, set as DataContext of its View, and return
    /// a BetterContentDialog with that View as its Content
    /// </summary>
    public static BetterContentDialog GetDialog<TService>(
        this IServiceManager<TService> serviceManager,
        Action<TService> initializer
    )
    {
        var instance = serviceManager.Get(initializer)!;

        if (
            Attribute.GetCustomAttribute(instance.GetType(), typeof(ViewAttribute))
            is not ViewAttribute viewAttr
        )
        {
            throw new InvalidOperationException($"View not found for {instance.GetType().FullName}");
        }

        if (Activator.CreateInstance(viewAttr.ViewType) is not Control view)
        {
            throw new NullReferenceException($"Unable to create instance for {instance.GetType().FullName}");
        }

        view.DataContext = instance;

        return new BetterContentDialog { Content = view };
    }
}
