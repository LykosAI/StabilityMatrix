using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using Octokit;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;
using Refit;
using Sentry;
using StabilityMatrix.Api;
using StabilityMatrix.Database;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;
using StabilityMatrix.Python;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Services;
using Application = System.Windows.Application;
using ConfigurationExtensions = NLog.ConfigurationExtensions;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;
using SnackbarService = StabilityMatrix.Helper.SnackbarService;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? serviceProvider;
        public static bool IsSentryEnabled => !Debugger.IsAttached || Environment
            .GetEnvironmentVariable("DEBUG_SENTRY")?.ToLowerInvariant() == "true";
        public static bool IsExceptionWindowEnabled => !Debugger.IsAttached || Environment
            .GetEnvironmentVariable("DEBUG_EXCEPTION_WINDOW")?.ToLowerInvariant() == "true";

        public static IConfiguration Config { get; set; }
        
        private readonly LoggingConfiguration logConfig;

        public App()
        {
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();
            // This needs to be done before OnStartup
            // Or Sentry will not be initialized correctly
            ConfigureErrorHandling();
            // Setup logging
            logConfig = ConfigureLogging();
        }

        private void ConfigureErrorHandling()
        {
            if (IsSentryEnabled)
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
                });
            }

            if (IsSentryEnabled || IsExceptionWindowEnabled)
            {
                DispatcherUnhandledException += App_DispatcherUnhandledException;
            }
        }

        private static LoggingConfiguration ConfigureLogging()
        {
            // Logging configuration
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
            var logConfig = new LoggingConfiguration();
            // File logging
            var fileTarget = new NLog.Targets.FileTarget("logfile") { FileName = logPath };
            // Log trace+ to debug console
            var debugTarget = new NLog.Targets.DebuggerTarget("debugger") { Layout = "${message}" };
            logConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, fileTarget);
            logConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, debugTarget);
            NLog.LogManager.Configuration = logConfig;
            // Add Sentry to NLog if enabled
            if (IsSentryEnabled)
            {
                ConfigurationExtensions.AddSentry(logConfig, o =>
                {
                    // Optionally specify a separate format for message
                    o.Layout = "${message}";
                    // Optionally specify a separate format for breadcrumbs
                    o.BreadcrumbLayout = "${logger}: ${message}";
                    // Debug and higher are stored as breadcrumbs (default is Info)
                    o.MinimumBreadcrumbLevel = NLog.LogLevel.Debug;
                    // Error and higher is sent as event (default is Error)
                    o.MinimumEventLevel = NLog.LogLevel.Error;
                });
            }

            return logConfig;
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddSingleton<IContentDialogService, ContentDialogService>();
            serviceCollection.AddSingleton<PageContentDialogService>();
            serviceCollection.AddSingleton<InstallerWindowDialogService>();
            serviceCollection.AddSingleton<UIState>();
            serviceCollection.AddSingleton<Wpf.Ui.Contracts.ISnackbarService, Wpf.Ui.Services.SnackbarService>();
            serviceCollection.AddSingleton<IPackageFactory, PackageFactory>();
            serviceCollection.AddSingleton<IPyRunner, PyRunner>();
            serviceCollection.AddSingleton<ISharedFolders, SharedFolders>();
            serviceCollection.AddTransient<IDialogFactory, DialogFactory>();
            
            serviceCollection.AddTransient<MainWindow>();
            serviceCollection.AddTransient<SettingsPage>();
            serviceCollection.AddTransient<LaunchPage>();
            serviceCollection.AddTransient<PackageManagerPage>();
            serviceCollection.AddTransient<TextToImagePage>();
            serviceCollection.AddTransient<CheckpointManagerPage>();
            serviceCollection.AddTransient<CheckpointBrowserPage>();
            serviceCollection.AddTransient<InstallerWindow>();
            serviceCollection.AddTransient<FirstLaunchSetupWindow>();

            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<SnackbarViewModel>();
            serviceCollection.AddTransient<LaunchOptionsDialogViewModel>();
            serviceCollection.AddSingleton<SettingsViewModel>();
            serviceCollection.AddSingleton<LaunchViewModel>();
            serviceCollection.AddSingleton<PackageManagerViewModel>();
            serviceCollection.AddSingleton<TextToImageViewModel>();
            serviceCollection.AddTransient<InstallerViewModel>();
            serviceCollection.AddTransient<OneClickInstallViewModel>();
            serviceCollection.AddTransient<CheckpointManagerViewModel>();
            serviceCollection.AddSingleton<CheckpointBrowserViewModel>();
            serviceCollection.AddSingleton<FirstLaunchSetupViewModel>();
            
            var settingsManager = new SettingsManager();
            serviceCollection.AddSingleton<ISettingsManager>(settingsManager);

            serviceCollection.AddSingleton<BasePackage, A3WebUI>();
            serviceCollection.AddSingleton<BasePackage, VladAutomatic>();
            serviceCollection.AddSingleton<BasePackage, ComfyUI>();
            serviceCollection.AddSingleton<Wpf.Ui.Contracts.ISnackbarService, Wpf.Ui.Services.SnackbarService>();
            serviceCollection.AddSingleton<IPrerequisiteHelper, PrerequisiteHelper>();
            serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
            serviceCollection.AddSingleton<INotificationBarService, NotificationBarService>();
            serviceCollection.AddSingleton<IDownloadService, DownloadService>();
            serviceCollection.AddTransient<IGitHubClient, GitHubClient>(_ =>
            {
                var client = new GitHubClient(new ProductHeaderValue("StabilityMatrix"));
                var githubApiKey = Config["GithubApiKey"];
                if (string.IsNullOrWhiteSpace(githubApiKey))
                    return client;
                
                client.Credentials = new Credentials(githubApiKey);
                return client;
            });
            serviceCollection.AddMemoryCache();
            serviceCollection.AddSingleton<IGithubApiCache, GithubApiCache>();

            // Setup LiteDb
            var connectionString = Config["TempDatabase"] switch
            {
                "True" => ":temp:",
                _ => settingsManager.DatabasePath
            };
            serviceCollection.AddSingleton<ILiteDbContext>(new LiteDbContext(connectionString));

            // Configure Refit and Polly
            var defaultRefitSettings = new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                })
            };
            
            var delay = Backoff
                .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: 5);
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(delay);

            // Add Refit clients
            serviceCollection.AddRefitClient<ICivitApi>(defaultRefitSettings)
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri("https://civitai.com");
                    c.Timeout = TimeSpan.FromSeconds(8);
                })
                .AddPolicyHandler(retryPolicy);

            serviceCollection.AddHttpClient("A3Client").AddPolicyHandler(retryPolicy);
            
            // Add Refit client managers
            serviceCollection.AddSingleton<IA3WebApiManager>(services =>
                new A3WebApiManager(services.GetRequiredService<ISettingsManager>(),
                    services.GetRequiredService<IHttpClientFactory>())
                {
                    RefitSettings = defaultRefitSettings,
                });
            
            // Add logging
            serviceCollection.AddLogging(log =>
            {
                log.ClearProviders();
                log.SetMinimumLevel(LogLevel.Trace);
                log.AddNLog(logConfig);
            });
            
            // Default error handling for 'SafeFireAndForget'
            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling(ex =>
            {
                var logger = serviceProvider?.GetRequiredService<ILogger<App>>();
                logger?.LogError(ex, "Background Task failed: {ExceptionMessage}", ex.Message);
            });

            // Insert path extensions
            settingsManager.InsertPathExtensions();
            
            serviceProvider = serviceCollection.BuildServiceProvider();

            // First time setup if needed
            if (!settingsManager.Settings.FirstLaunchSetupComplete)
            {
                var setupWindow = serviceProvider.GetRequiredService<FirstLaunchSetupWindow>();
                if (setupWindow.ShowDialog() ?? false)
                {
                    settingsManager.SetFirstLaunchSetupComplete(true);
                }
                else
                {
                    Current.Shutdown();
                    return;
                }
            }
            
            var window = serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            serviceProvider?.GetRequiredService<LaunchViewModel>().OnShutdown();
            serviceProvider?.GetRequiredService<ILiteDbContext>().Dispose();
        }
        
        [DoesNotReturn]
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (SentrySdk.IsEnabled)
            {
                SentrySdk.CaptureException(e.Exception);
            }
            
            var logger = serviceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogCritical(e.Exception, "Unhandled Exception: {ExceptionMessage}", e.Exception.Message);
            
            if (IsExceptionWindowEnabled)
            {
                var vm = new ExceptionWindowViewModel
                {
                    Exception = e.Exception
                };
                var exceptionWindow = new ExceptionWindow
                {
                    DataContext = vm,
                    Owner = MainWindow
                };
                exceptionWindow.ShowDialog();
            }
            e.Handled = true;
            Current.Shutdown(1);
            Environment.Exit(1);
        }
    }
}

