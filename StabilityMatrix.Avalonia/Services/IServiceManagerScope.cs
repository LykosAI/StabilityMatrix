namespace StabilityMatrix.Avalonia.Services;

public interface IServiceManagerScope<T> : IDisposable
{
    /// <summary>
    /// Provides access to an instance of IServiceManager for managing and retrieving services
    /// of a specified type T within the associated scope.
    /// </summary>
    IServiceManager<T> ServiceManager { get; }
}
