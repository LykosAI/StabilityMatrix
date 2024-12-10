using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Core.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets all managed <see cref="IDisposable"/> services from the <see cref="ServiceProvider"/>.
    /// Accesses the private field `Root[ServiceProviderEngineScope]._disposables[List&lt;object&gt;?]`.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    public static List<object> GetDisposables(this ServiceProvider serviceProvider)
    {
        // ServiceProvider: internal ServiceProviderEngineScope Root { get; }
        var root =
            serviceProvider.GetProtectedProperty("Root")
            ?? throw new InvalidOperationException("Could not get ServiceProviderEngineScope Root.");

        // ServiceProviderEngineScope: private List<object>? _disposables
        var disposables = root.GetPrivateField<List<object>?>("_disposables");

        return disposables ?? [];
    }
}
