using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Windowing;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public class AppWindowBase : AppWindow
{
    public CancellationTokenSource? ShowAsyncCts { get; set; }
    
    protected AppWindowBase()
    {
    }
    
    public void ShowWithCts(CancellationTokenSource cts)
    {
        ShowAsyncCts?.Cancel();
        ShowAsyncCts = cts;
        Show();
    }

    public Task ShowAsync()
    {
        ShowAsyncCts?.Cancel();
        ShowAsyncCts = new CancellationTokenSource();
        
        var tcs = new TaskCompletionSource<bool>();
        ShowAsyncCts.Token.Register(s =>
        {
            ((TaskCompletionSource<bool>) s!).SetResult(true);
        }, tcs);
        
        Show();
        
        return tcs.Task;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (ShowAsyncCts is not null)
        {
            ShowAsyncCts.Cancel();
            ShowAsyncCts = null;
        }
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
