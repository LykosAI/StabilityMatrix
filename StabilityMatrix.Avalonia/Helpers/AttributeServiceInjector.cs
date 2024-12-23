using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Registers services using <see cref="TransientAttribute"/> and <see cref="SingletonAttribute"/> attributes.
/// </summary>
[Localizable(false)]
internal static partial class AttributeServiceInjector
{
    /// <summary>
    /// Registers services from the assemblies using Attributes via source generation.
    /// - Uses Source Generation by default (<see cref="AddServicesByAttributesSourceGen"/>).
    /// If `REGISTER_SERVICE_REFLECTION` symbol is defined, (also requires `REGISTER_SERVICE_USAGES` symbol) then:
    /// - Uses Reflection (<see cref="AddServicesByAttributesReflection"/>).
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which the services will be registered.
    /// </param>
    public static IServiceCollection AddServicesByAttributes(this IServiceCollection services)
    {
#if REGISTER_SERVICE_REFLECTION
        var assemblies = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("StabilityMatrix") == true);
        AddServicesByAttributesReflection(services, assemblies);
#else
        AddServicesByAttributesSourceGen(services);
#endif
        return services;
    }

    /// <summary>
    /// Registers services from the assemblies using Attributes via source generation.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which the services will be registered.
    /// </param>
    public static void AddServicesByAttributesSourceGen(IServiceCollection services)
    {
        services.AddStabilityMatrixCore();
        services.AddStabilityMatrixAvalonia();
    }

    /// <summary>
    /// Adds a <see cref="ServiceManager{T}"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the <see cref="ServiceManager{T}"/> will be added.</param>
    /// <param name="serviceFilter">An optional filter for the services.</param>
    /// <typeparam name="TService">The base type of the services.</typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddServiceManagerWithCurrentCollectionServices<TService>(
        this IServiceCollection services,
        Func<ServiceDescriptor, bool>? serviceFilter = null
    )
    {
        return services.AddSingleton<ServiceManager<TService>>(provider =>
        {
            using var _ = CodeTimer.StartDebug(
                callerName: $"{nameof(AddServiceManagerWithCurrentCollectionServices)}<{typeof(TService)}>"
            );

            var serviceManager = new ServiceManager<TService>();

            // Get registered services that are assignable to TService
            var serviceDescriptors = services.Where(s => s.ServiceType.IsAssignableTo(typeof(TService)));

            // Optional filter
            if (serviceFilter is not null)
            {
                serviceDescriptors = serviceDescriptors.Where(serviceFilter);
            }

            foreach (var service in serviceDescriptors)
            {
                var type = service.ServiceType;
                Debug.Assert(type is not null, "type is not null");
                Debug.Assert(type.IsAssignableTo(typeof(TService)), "type is assignable to TService");

                serviceManager.Register(type, () => (TService)provider.GetRequiredService(type));
            }

            return serviceManager;
        });
    }
}
