using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Web.WebView2.Core;
using NLog;

namespace StabilityMatrix.ViewModels;

public record struct NavigationResult(Uri? Uri, List<CoreWebView2Cookie>? Cookies);

public partial class WebLoginViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Login Url, set externally on dialog creation
    [ObservableProperty] private string? loginUrl;
    // Bound current url source
    [ObservableProperty] private Uri? currentUri;
    // Always true after first navigation completed
    [ObservableProperty] private bool isContentLoaded;
    
    // Events
    public event EventHandler<NavigationResult> NavigationCompleted = delegate { };
    public event EventHandler<NavigationResult> SourceChanged = delegate { };

    public void OnLoaded()
    {
    }

    /// <summary>
    /// Called on navigation source changes.
    /// </summary>
    public void OnSourceChanged(Uri? source, List<CoreWebView2Cookie>? cookies)
    {
        Logger.Debug($"WebView source changed to {source} ({cookies?.Count} cookies)");
        SourceChanged.Invoke(this, new NavigationResult(source, cookies));
    }
    
    /// <summary>
    /// Called on navigation completed. (After scrollbar patch)
    /// </summary>
    public void OnNavigationCompleted(Uri? uri)
    {
        Logger.Debug($"WebView loaded: {uri}");
        NavigationCompleted.Invoke(this, new NavigationResult(uri, null));
        IsContentLoaded = true;
    }
    
    /// <summary>
    /// Waits for navigation to a specific uri
    /// </summary>
    public async Task WaitForNavigation(Uri uri, CancellationToken ct = default)
    {
        Logger.Debug($"Waiting for navigation to {uri}");
        
        var navigationTask = new TaskCompletionSource<bool>();
        
        var handler = new EventHandler<NavigationResult>((_, result) =>
        {
            navigationTask.TrySetResult(true);
        });
        
        NavigationCompleted += handler;
        try
        {
            await using (ct.Register(() => navigationTask.TrySetCanceled()))
            {
                CurrentUri = uri;
                await navigationTask.Task;
            }
        }
        finally
        {
            NavigationCompleted -= handler;
        }
    }
}
