using System.Collections.Concurrent;

namespace StabilityMatrix.Avalonia.Services;

internal class ScopedServiceManager<T> : IServiceManager<T>, IDisposable
{
    private readonly ServiceManager<T> parentManager;
    private readonly IServiceProvider scopedServiceProvider;
    private readonly ConcurrentDictionary<Type, T> _scopedInstances = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = new();
    private readonly Lock _lock = new(); // For creating scoped instances safely

    private bool _disposed;

    internal ScopedServiceManager(ServiceManager<T> parentManager, IServiceProvider scopedServiceProvider)
    {
        this.parentManager = parentManager;
        this.scopedServiceProvider = scopedServiceProvider;

        // Add ourselves as the materialized IServiceManager<T> for this scope
        // _scopedInstances[typeof(IServiceManager<T>)] = this;
    }

    // Delegate Register methods to the parent manager

    public IServiceManager<T> Register<TService>(TService instance)
        where TService : T
    {
        return parentManager.Register(instance);
    }

    public IServiceManager<T> Register<TService>(Func<TService> provider)
        where TService : T
    {
        return parentManager.Register(provider);
    }

    public void Register(Type type, Func<T> providerFunc)
    {
        parentManager.Register(type, providerFunc);
    }

    public IServiceManager<T> RegisterProvider<TService>(IServiceProvider provider)
        where TService : notnull, T
    {
        return parentManager.RegisterProvider<TService>(provider);
    }

    public IServiceManager<T> RegisterScoped<TService>(Func<IServiceProvider, TService> provider)
        where TService : T
    {
        return parentManager.RegisterScoped(provider);
    }

    public IServiceManager<T> RegisterScoped(Type type, Func<IServiceProvider, T> provider)
    {
        return parentManager.RegisterScoped(type, provider);
    }

    public IServiceManagerScope<T> CreateScope()
    {
        return parentManager.CreateScope();
    }

    public TService Get<TService>()
        where TService : T
    {
        return (TService)Get(typeof(TService))!;
    }

    public T Get(Type serviceType)
    {
        if (!typeof(T).IsAssignableFrom(serviceType)) // Ensure type compatibility
        {
            throw new ArgumentException($"Service type {serviceType} is not assignable to {typeof(T)}");
        }

        // 1. Check if instance already exists *in this scope*
        if (_scopedInstances.TryGetValue(serviceType, out var scopedInstance))
        {
            return scopedInstance;
        }

        // 2. Check if it's a known *scoped* service type from the parent
        if (parentManager.TryGetScopedProvider(serviceType, out var scopedProvider))
        {
            // Lock to prevent multiple creations of the same scoped service concurrently within this scope
            lock (_lock)
            {
                // Double-check if another thread created it while waiting for the lock
                if (_scopedInstances.TryGetValue(serviceType, out scopedInstance))
                {
                    return scopedInstance;
                }

                // Create the scoped instance using the factory from the parent
                var newScopedInstance = scopedProvider(scopedServiceProvider);
                if (newScopedInstance == null)
                    throw new InvalidOperationException($"Scoped provider for {serviceType} returned null.");

                if (!_scopedInstances.TryAdd(serviceType, newScopedInstance))
                {
                    // Should not happen due to outer check + lock, but defensive check
                    throw new InvalidOperationException(
                        $"Failed to add scoped instance for {serviceType}. Concurrency issue?"
                    );
                }

                // Track disposables created within this scope
                if (newScopedInstance is IDisposable disposable)
                    _disposables.Add(disposable);
                if (newScopedInstance is IAsyncDisposable asyncDisposable)
                    _asyncDisposables.Add(asyncDisposable);

                return newScopedInstance;
            }
        }

        // 3. If not scoped, delegate to the parent manager to resolve Singleton or Transient
        //    (Parent's Get will throw if the type isn't registered there either)
        return parentManager.Get(serviceType);
    }

    public void Dispose()
    {
        // Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose synchronous disposables created *within this scope*
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                { /* Log error */
                }
            }
            _disposables.Clear();

            // Handle async disposables created *within this scope*
            foreach (var asyncDisposable in _asyncDisposables)
            {
                try
                {
                    asyncDisposable.DisposeAsync().AsTask().Wait();
                }
                catch (Exception ex)
                { /* Log error */
                }
            }
            _asyncDisposables.Clear();

            _scopedInstances.Clear(); // Clear instances held by this scope
        }
        _disposed = true;
    }
}
