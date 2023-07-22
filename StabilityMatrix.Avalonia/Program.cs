using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NLog;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Sentry;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;

namespace StabilityMatrix.Avalonia;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Program
{
    private static bool isExceptionDialogEnabled;
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure exception dialog for unhandled exceptions
        if (!Debugger.IsAttached || args.Contains("--debug-exception-dialog"))
        {
            isExceptionDialogEnabled = true;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }
        
        // Configure Sentry
        if ((Debugger.IsAttached && args.Contains("--debug-sentry")) || !args.Contains("--no-sentry"))
        {
            ConfigureSentry();
        }
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
    
    private static void ConfigureSentry()
    {
        SentrySdk.Init(o =>
        {
            o.Dsn = "https://eac7a5ea065d44cf9a8565e0f1817da2@o4505314753380352.ingest.sentry.io/4505314756067328";
            o.StackTraceMode = StackTraceMode.Enhanced;
            o.TracesSampleRate = 1.0;
            o.IsGlobalModeEnabled = true;
            // Enables Sentry's "Release Health" feature.
            o.AutoSessionTracking = true;
            // 1.0 to capture 100% of transactions for performance monitoring.
            o.TracesSampleRate = 1.0;
#if DEBUG
            o.Environment = "Development";
#endif
        });
    }
    
    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex) return;
        
        var logger = LogManager.GetCurrentClassLogger();
        logger.Fatal(ex, "Unhandled {Type}: {Message}", ex.GetType().Name, ex.Message);
        
        if (SentrySdk.IsEnabled)
        {
            SentrySdk.CaptureException(ex);
        }
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var dialog = new ExceptionDialog
            {
                DataContext = new ExceptionViewModel
                {
                    Exception = ex
                }
            };
                
            var mainWindow = lifetime.MainWindow;
            // We can only show dialog if main window exists, and is visible
            if (mainWindow is {PlatformImpl: not null, IsVisible: true})
            {
                // Configure for dialog mode
                dialog.ShowAsDialog = true;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    
                // Show synchronously without blocking UI thread
                // https://github.com/AvaloniaUI/Avalonia/issues/4810#issuecomment-704259221
                var cts = new CancellationTokenSource();
                    
                dialog.ShowDialog(mainWindow).ContinueWith(_ =>
                {
                    cts.Cancel();
                    ExitWithException(ex);
                }, TaskScheduler.FromCurrentSynchronizationContext());
                    
                Dispatcher.UIThread.MainLoop(cts.Token);
            }
            else
            {
                // No parent window available
                var cts = new CancellationTokenSource();
                // Exit on token cancellation
                cts.Token.Register(() => ExitWithException(ex));
                
                dialog.ShowWithCts(cts);
                
                Dispatcher.UIThread.MainLoop(cts.Token);
            }
        }
    }

    [DoesNotReturn]
    private static void ExitWithException(Exception exception)
    {
        App.Shutdown(1);
        Dispatcher.UIThread.InvokeShutdown();
        Environment.Exit(Marshal.GetHRForException(exception));
    }
}
