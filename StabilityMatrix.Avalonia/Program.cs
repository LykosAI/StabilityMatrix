using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Polly.Contrib.WaitAndRetry;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Semver;
using Sentry;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Avalonia;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Program
{
    private static Logger? _logger;
    private static Logger Logger => _logger ??= LogManager.GetCurrentClassLogger();
    
    public static AppArgs Args { get; } = new();
    
    public static bool IsDebugBuild { get; private set; }
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Args.DebugExceptionDialog = args.Contains("--debug-exception-dialog");
        Args.DebugSentry = args.Contains("--debug-sentry");
        Args.NoSentry = args.Contains("--no-sentry");
        Args.NoWindowChromeEffects = args.Contains("--no-window-chrome-effects");
        Args.ResetWindowPosition = args.Contains("--reset-window-position");

        SetDebugBuild();
        
        HandleUpdateReplacement();
        
        var infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Compat.AppVersion = SemVersion.Parse(infoVersion ?? "0.0.0", SemVersionStyles.Strict);
        
        // Configure exception dialog for unhandled exceptions
        if (!Debugger.IsAttached || Args.DebugExceptionDialog)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }
        
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        
        // Configure Sentry
        if (!Args.NoSentry && (!Debugger.IsAttached || Args.DebugSentry))
        {
            ConfigureSentry();
        }
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        // Use our custom image loader for custom local load error handling
        ImageLoader.AsyncImageLoader.Dispose();
        ImageLoader.AsyncImageLoader = new FallbackRamCachedWebImageLoader();
        
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static void HandleUpdateReplacement()
    {
        // Check if we're in the named update folder or the legacy update folder for 1.2.0 -> 2.0.0
        if (Compat.AppCurrentDir is {Name: UpdateHelper.UpdateFolderName} or {Name: "Update"})
        {
            var parentDir = Compat.AppCurrentDir.Parent;
            if (parentDir is null) 
                return;
            
            var retryDelays = Backoff.DecorrelatedJitterBackoffV2(
                TimeSpan.FromMilliseconds(350), retryCount: 5);

            foreach (var delay in retryDelays)
            {
                // Copy our current file to the parent directory, overwriting the old app file
                var currentExe = Compat.AppCurrentDir.JoinFile(Compat.GetExecutableName());
                var targetExe = parentDir.JoinFile(Compat.GetExecutableName());
                try
                {
                    currentExe.CopyTo(targetExe, true);
                    
                    // Ensure permissions are set for unix
                    if (Compat.IsUnix)
                    {
                        File.SetUnixFileMode(targetExe, (UnixFileMode) 0x755);
                    }
                    
                    // Start the new app
                    Process.Start(targetExe);
                    
                    // Shutdown the current app
                    Environment.Exit(0);
                }
                catch (Exception)
                {
                    Thread.Sleep(delay);
                }
            }
        }
        
        // Delete update folder if it exists in current directory
        var updateDir = UpdateHelper.UpdateFolder;
        if (updateDir.Exists)
        {
            try
            {
                updateDir.Delete(true);
            }
            catch (Exception e)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(e, "Failed to delete update file");
            }
        }
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

    private static void TaskScheduler_UnobservedTaskException(object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception is Exception ex)
        {
            Logger.Error(ex, "Unobserved task exception");
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex) return;
        
        Logger.Fatal(ex, "Unhandled {Type}: {Message}", ex.GetType().Name, ex.Message);
        
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

    [Conditional("DEBUG")]
    private static void SetDebugBuild()
    {
        IsDebugBuild = true;
    }
}
