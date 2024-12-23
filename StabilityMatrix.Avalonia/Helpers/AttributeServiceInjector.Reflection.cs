using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Helpers;

internal static partial class AttributeServiceInjector
{
    /// <summary>
    /// Registers services from the assemblies starting with "StabilityMatrix" using
    /// <see cref="TransientAttribute"/> and <see cref="SingletonAttribute"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which the services will be registered.
    /// </param>
    /// <param name="assemblies">
    /// The assemblies to scan for services.
    /// </param>
    // Injectio.Attributes are conditional with the required `REGISTER_SERVICE_USAGES` compilation symbol
    [Conditional("REGISTER_SERVICE_USAGES")]
    public static void AddServicesByAttributesReflection(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies
    )
    {
        var exportedTypes = assemblies.SelectMany(a => a.GetExportedTypes()).ToArray();

        var transientTypes = exportedTypes
            .Select(
                t => new { t, attributes = t.GetCustomAttributes(typeof(RegisterTransientAttribute), false) }
            )
            .Where(t1 => t1.attributes is { Length: > 0 })
            .Select(t1 => new { Type = t1.t, Attribute = (RegisterTransientAttribute)t1.attributes[0] })
            .ToArray();

        foreach (var typePair in transientTypes)
        {
            if (
                typePair.Attribute.ServiceType is not null
                && typePair.Attribute.ImplementationType is not null
            )
            {
                services.AddTransient(typePair.Attribute.ServiceType, typePair.Attribute.ImplementationType);
            }
            else if (typePair.Attribute.ServiceType is not null)
            {
                services.AddTransient(typePair.Attribute.ServiceType, typePair.Type);
            }
            else
            {
                services.AddTransient(typePair.Type);
            }
        }

        var singletonTypes = exportedTypes
            .Select(
                t => new { t, attributes = t.GetCustomAttributes(typeof(RegisterSingletonAttribute), false) }
            )
            .Where(
                t1 =>
                    t1.attributes is { Length: > 0 }
                    && !t1.t.Name.Contains("Mock", StringComparison.OrdinalIgnoreCase)
            )
            .Select(
                t1 =>
                    new
                    {
                        Type = t1.t,
                        Attributes = t1.attributes.Cast<RegisterSingletonAttribute>().ToArray()
                    }
            )
            .ToArray();

        foreach (var typePair in singletonTypes)
        {
            foreach (var attribute in typePair.Attributes)
            {
                if (attribute.ServiceType is not null && attribute.ImplementationType is not null)
                {
                    services.AddSingleton(attribute.ServiceType, attribute.ImplementationType);
                }
                else if (attribute.ServiceType is not null)
                {
                    services.AddSingleton(attribute.ServiceType, typePair.Type);
                }
                else
                {
                    services.AddSingleton(typePair.Type);
                }
            }
        }
    }

    /// <summary>
    /// Registers services from the assemblies starting with "StabilityMatrix" using
    /// <see cref="TransientAttribute"/> and <see cref="SingletonAttribute"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which the services will be registered.
    /// </param>
    /// <param name="assemblies">
    /// The assemblies to scan for services.
    /// </param>
    public static void AddServicesByAttributesReflectionOld(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies
    )
    {
        var exportedTypes = assemblies.SelectMany(a => a.GetExportedTypes()).ToArray();

        var transientTypes = exportedTypes
            .Select(t => new { t, attributes = t.GetCustomAttributes(typeof(TransientAttribute), false) })
            .Where(
                t1 =>
                    t1.attributes is { Length: > 0 }
                    && !t1.t.Name.Contains("Mock", StringComparison.OrdinalIgnoreCase)
            )
            .Select(t1 => new { Type = t1.t, Attribute = (TransientAttribute)t1.attributes[0] });

        foreach (var typePair in transientTypes)
        {
            if (typePair.Attribute.InterfaceType is null)
            {
                services.AddTransient(typePair.Type);
            }
            else
            {
                services.AddTransient(typePair.Attribute.InterfaceType, typePair.Type);
            }
        }

        var singletonTypes = exportedTypes
            .Select(t => new { t, attributes = t.GetCustomAttributes(typeof(SingletonAttribute), false) })
            .Where(
                t1 =>
                    t1.attributes is { Length: > 0 }
                    && !t1.t.Name.Contains("Mock", StringComparison.OrdinalIgnoreCase)
            )
            .Select(
                t1 => new { Type = t1.t, Attributes = t1.attributes.Cast<SingletonAttribute>().ToArray() }
            );

        foreach (var typePair in singletonTypes)
        {
            foreach (var attribute in typePair.Attributes)
            {
                if (attribute.InterfaceType is null)
                {
                    services.AddSingleton(typePair.Type);
                }
                else if (attribute.ImplType is not null)
                {
                    services.AddSingleton(attribute.InterfaceType, attribute.ImplType);
                }
                else
                {
                    services.AddSingleton(attribute.InterfaceType, typePair.Type);
                }

                // IDisposable registering
                var serviceType = attribute.InterfaceType ?? typePair.Type;

                if (serviceType == typeof(IDisposable) || serviceType == typeof(IAsyncDisposable))
                {
                    continue;
                }

                if (typePair.Type.IsAssignableTo(typeof(IDisposable)))
                {
                    Debug.WriteLine("Registering IDisposable: {Name}", typePair.Type.Name);
                    services.AddSingleton<IDisposable>(
                        provider => (IDisposable)provider.GetRequiredService(serviceType)
                    );
                }

                if (typePair.Type.IsAssignableTo(typeof(IAsyncDisposable)))
                {
                    Debug.WriteLine("Registering IAsyncDisposable: {Name}", typePair.Type.Name);
                    services.AddSingleton<IAsyncDisposable>(
                        provider => (IAsyncDisposable)provider.GetRequiredService(serviceType)
                    );
                }
            }
        }
    }
}
