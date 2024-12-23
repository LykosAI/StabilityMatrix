using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private static void SafeDispose(
        this ServiceProvider serviceProvider,
        TimeSpan timeoutTotal,
        TimeSpan timeoutPerDispose,
        ILogger logger
    )
    {
        var timeoutTotalCts = new CancellationTokenSource(timeoutTotal);

        // Dispose services
        var toDispose = serviceProvider.GetDisposables().OfType<IDisposable>().ToImmutableList();

        logger.LogDebug("OnExit: Preparing to Dispose {Count} Services", toDispose.Count);

        // Dispose IDisposable services
        foreach (var disposable in toDispose)
        {
            logger.LogTrace("OnExit: Disposing {Name}", disposable.GetType().Name);

            using var instanceCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutTotalCts.Token,
                new CancellationTokenSource(timeoutPerDispose).Token
            );

            try
            {
                Task.Run(() => disposable.Dispose(), instanceCts.Token).Wait(instanceCts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("OnExit: Timeout disposing {Name}", disposable.GetType().Name);
            }
            catch (Exception e)
            {
                logger.LogError(e, "OnExit: Failed to dispose {Name}", disposable.GetType().Name);
            }
        }
    }

    private static async ValueTask SafeDisposeAsync(
        this ServiceProvider serviceProvider,
        TimeSpan timeoutTotal,
        TimeSpan timeoutPerDispose,
        ILogger logger
    )
    {
        var timeoutTotalCts = new CancellationTokenSource(timeoutTotal);

        // Dispose services
        var toDispose = serviceProvider.GetDisposables().OfType<IDisposable>().ToImmutableList();

        // Dispose IDisposable services
        foreach (var disposable in toDispose)
        {
            logger.LogTrace("Disposing {Name}", disposable.GetType().Name);

            using var instanceCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutTotalCts.Token,
                new CancellationTokenSource(timeoutPerDispose).Token
            );

            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable
                        .DisposeAsync()
                        .AsTask()
                        .WaitAsync(instanceCts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Task.Run(() => disposable.Dispose(), instanceCts.Token)
                        .WaitAsync(instanceCts.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timeout disposing {Name}", disposable.GetType().Name);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to dispose {Name}", disposable.GetType().Name);
            }
        }
    }
}
