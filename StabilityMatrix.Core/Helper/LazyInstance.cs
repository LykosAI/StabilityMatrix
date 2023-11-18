using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Lazy instance of a DI service.
/// </summary>
public class LazyInstance<T> : Lazy<T>
    where T : notnull
{
    public LazyInstance(IServiceProvider serviceProvider)
        : base(serviceProvider.GetRequiredService<T>) { }
}

public static class LazyInstanceServiceExtensions
{
    /// <summary>
    /// Register <see cref="LazyInstance{T}"/> to be used when resolving <see cref="Lazy{T}"/> instances.
    /// </summary>
    public static IServiceCollection AddLazyInstance(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazyInstance<>));
        return services;
    }
}
