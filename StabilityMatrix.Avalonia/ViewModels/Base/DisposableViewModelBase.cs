using System;
using System.Reactive.Disposables;
using JetBrains.Annotations;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public abstract class DisposableViewModelBase : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable instanceDisposables = new();

    /// <summary>
    /// Adds a disposable to be disposed when this view model is disposed.
    /// </summary>
    /// <param name="disposable">The disposable to add.</param>
    protected void AddDisposable([HandlesResourceDisposal] IDisposable disposable)
    {
        instanceDisposables.Add(disposable);
    }

    /// <summary>
    /// Adds disposables to be disposed when this view model is disposed.
    /// </summary>
    /// <param name="disposables">The disposables to add.</param>
    protected void AddDisposable([HandlesResourceDisposal] params IDisposable[] disposables)
    {
        foreach (var disposable in disposables)
        {
            instanceDisposables.Add(disposable);
        }
    }

    /// <summary>
    /// Adds a disposable to be disposed when this view model is disposed.
    /// </summary>
    /// <param name="disposable">The disposable to add.</param>
    /// <typeparam name="T">The type of the disposable.</typeparam>
    /// <returns>The disposable that was added.</returns>
    protected T AddDisposable<T>([HandlesResourceDisposal] T disposable)
        where T : IDisposable
    {
        instanceDisposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Adds disposables to be disposed when this view model is disposed.
    /// </summary>
    /// <param name="disposables">The disposables to add.</param>
    /// <typeparam name="T">The type of the disposables.</typeparam>
    /// <returns>The disposables that were added.</returns>
    protected T[] AddDisposable<T>([HandlesResourceDisposal] params T[] disposables)
        where T : IDisposable
    {
        foreach (var disposable in disposables)
        {
            instanceDisposables.Add(disposable);
        }

        return disposables;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            instanceDisposables.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
