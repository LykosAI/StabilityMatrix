using System.Diagnostics.CodeAnalysis;
using AsyncAwaitBestPractices;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public class AppWindowBase : AppWindow
{
    protected AppWindowBase()
    {
    }
    
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is ViewModelBase viewModel)
        {
            // Run synchronous load then async load
            viewModel.OnLoaded();
        
            // Can't block here so we'll run as async on UI thread
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await viewModel.OnLoadedAsync();
            }).SafeFireAndForget();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        if (DataContext is not ViewModelBase viewModel)
            return;
        
        // Run synchronous load then async unload
        viewModel.OnUnloaded();
        
        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await viewModel.OnUnloadedAsync();
        }).SafeFireAndForget();
    }
}
