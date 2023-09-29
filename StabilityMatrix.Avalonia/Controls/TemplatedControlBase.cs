using AsyncAwaitBestPractices;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls;

public abstract class TemplatedControlBase : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is not ViewModelBase viewModel)
            return;

        // Run synchronous load then async load
        viewModel.OnLoaded();

        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(viewModel.OnLoadedAsync).SafeFireAndForget();
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (DataContext is not ViewModelBase viewModel)
            return;

        // Run synchronous load then async load
        viewModel.OnUnloaded();

        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(viewModel.OnUnloadedAsync).SafeFireAndForget();
    }
}
