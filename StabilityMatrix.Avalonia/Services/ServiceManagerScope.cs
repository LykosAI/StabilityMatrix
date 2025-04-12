using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Avalonia.Services;

internal class ServiceManagerScope<T>(
    [HandlesResourceDisposal] IServiceScope scope,
    [HandlesResourceDisposal] ScopedServiceManager<T> serviceManager
) : IServiceManagerScope<T>
{
    public IServiceManager<T> ServiceManager { get; } = serviceManager;

    public void Dispose()
    {
        scope.Dispose();
        serviceManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
