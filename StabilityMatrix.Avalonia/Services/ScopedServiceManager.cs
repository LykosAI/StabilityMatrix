using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Avalonia.Services;

internal class ScopedServiceManager<T> : IServiceManager<T>
{
    private readonly ServiceManager<T> parentManager;
    private readonly IServiceProvider scopedServiceProvider;

    internal ScopedServiceManager(ServiceManager<T> parentManager, IServiceProvider scopedServiceProvider)
    {
        this.parentManager = parentManager;
        this.scopedServiceProvider = scopedServiceProvider;
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

        // Check if it's a known *scoped* service type from the parent
        if (parentManager.TryGetScopedProvider(serviceType, out var scopedProvider))
        {
            // Create the scoped instance using the factory from the parent
            var newScopedInstance = scopedProvider(scopedServiceProvider);
            if (newScopedInstance == null)
                throw new InvalidOperationException($"Scoped provider for {serviceType} returned null.");

            return newScopedInstance;
        }

        // 3. If not scoped, delegate to the parent manager to resolve Singleton or Transient
        //    (Parent's Get will throw if the type isn't registered there either)
        // return parentManager.Get(serviceType);

        // We don't use parent manager for scoped contexts anymore,
        // since we'll lose the scope through transients,
        // then we have to make Inference Cards scoped as well,
        // which cases samplers to be shared with Civit page and other issues.
        // 3. Just use the scoped service provider, since we might need to keep the scope through transients as well
        return (T)scopedServiceProvider.GetRequiredService(serviceType);
    }
}
