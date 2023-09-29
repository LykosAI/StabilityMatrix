using System.Diagnostics.CodeAnalysis;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public class UserControlBase : UserControl
{
    static UserControlBase()
    {
        LoadedEvent.AddClassHandler<UserControlBase>((cls, args) => cls.OnLoadedEvent(args));

        UnloadedEvent.AddClassHandler<UserControlBase>((cls, args) => cls.OnUnloadedEvent(args));
    }

    // ReSharper disable once UnusedParameter.Global
    protected virtual void OnLoadedEvent(RoutedEventArgs? e)
    {
        if (DataContext is not ViewModelBase viewModel)
            return;

        // Run synchronous load then async load
        viewModel.OnLoaded();

        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(viewModel.OnLoadedAsync).SafeFireAndForget();
    }

    // ReSharper disable once UnusedParameter.Global
    protected virtual void OnUnloadedEvent(RoutedEventArgs? e)
    {
        if (DataContext is not ViewModelBase viewModel)
            return;

        // Run synchronous load then async load
        viewModel.OnUnloaded();

        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(viewModel.OnUnloadedAsync).SafeFireAndForget();
    }
}
