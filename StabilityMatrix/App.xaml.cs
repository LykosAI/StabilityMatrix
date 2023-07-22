using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using Octokit;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;
using Refit;
using Sentry;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Services;
using Application = System.Windows.Application;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using SnackbarService = StabilityMatrix.Helper.SnackbarService;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private ServiceProvider? serviceProvider;

        // ReSharper disable once MemberCanBePrivate.Global
        public static bool IsSentryEnabled => !Debugger.IsAttached || Environment
            .GetEnvironmentVariable("DEBUG_SENTRY")?.ToLowerInvariant() == "true";
        // ReSharper disable once MemberCanBePrivate.Global
        public static bool IsExceptionWindowEnabled => !Debugger.IsAttached || Environment
            .GetEnvironmentVariable("DEBUG_EXCEPTION_WINDOW")?.ToLowerInvariant() == "true";

        public static IConfiguration Config { get; set; } = null!;

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
#if DEBUG
                    o.Environment = "Development";
#endif
                });
            }

            if (IsSentryEnabled || IsExceptionWindowEnabled)
            {
                DispatcherUnhandledException += App_DispatcherUnhandledException;
            }
        }

        private static LoggingConfiguration ConfigureLogging()
        {
            var logConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget("logfile")
            {
                ArchiveOldFileOnStartup = true,
                FileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.log",
                ArchiveFileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.{#}.log",
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 2
            };
            var debugTarget = new DebuggerTarget("debugger") { Layout = "${message}" };
            logConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, fileTarget);
            logConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, debugTarget);

            NLog.LogManager.Configuration = logConfig;
            // Add Sentry to NLog if enabled
            if (IsSentryEnabled)
            {
                logConfig.AddSentry(o =>
                {
                    o.InitializeSdk = false;
                    o.Layout = "${message}";
                    o.IncludeEventDataOnBreadcrumbs = true;
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
            if (AppDomain.CurrentDomain.BaseDirectory.EndsWith("Update\\"))
            {
                var delays = Backoff.DecorrelatedJitterBackoffV2(
                    TimeSpan.FromMilliseconds(150), retryCount: 3);
                foreach (var dlay in delays) 
                {
                    try
                    {
                        File.Copy(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "StabilityMatrix.exe"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
                                "StabilityMatrix.exe"), true);
                        
                        Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
                            "StabilityMatrix.exe"));
                        
                        Current.Shutdown();
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(dlay);
                    }
                }
                return;
            }

            var updateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");
            if (Directory.Exists(updateDir))
            {
                try
                {
                    Directory.Delete(updateDir, true);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Failed to delete update file");
                }
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddSingleton<IContentDialogService, ContentDialogService>();
            serviceCollection.AddSingleton<PageContentDialogService>();
            serviceCollection.AddSingleton<InstallerWindowDialogService>();
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
            serviceCollection.AddTransient<UpdateWindowViewModel>();
            serviceCollection.AddTransient<InstallerViewModel>();
            serviceCollection.AddTransient<SelectInstallLocationsViewModel>();
            serviceCollection.AddTransient<DataDirectoryMigrationViewModel>();
            serviceCollection.AddTransient<WebLoginViewModel>();
            serviceCollection.AddTransient<OneClickInstallViewModel>();
            serviceCollection.AddTransient<CheckpointManagerViewModel>();
            serviceCollection.AddSingleton<CheckpointBrowserViewModel>();
            serviceCollection.AddSingleton<FirstLaunchSetupViewModel>();

            serviceCollection.Configure<DebugOptions>(Config.GetSection(nameof(DebugOptions)));

            serviceCollection.AddSingleton<IUpdateHelper, UpdateHelper>();
            serviceCollection.AddSingleton<ISettingsManager, SettingsManager>();
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
            serviceCollection.AddSingleton<ModelFinder>();

            // Database
            serviceCollection.AddSingleton<ILiteDbContext, LiteDbContext>();

            // Caches
            serviceCollection.AddMemoryCache();
            serviceCollection.AddSingleton<IGithubApiCache, GithubApiCache>();

            // Configure Refit and Polly
            var defaultSystemTextJsonSettings =
                SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions();
            defaultSystemTextJsonSettings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            var defaultRefitSettings = new RefitSettings
            {
                ContentSerializer =
                    new SystemTextJsonContentSerializer(defaultSystemTextJsonSettings),
            };

            // HTTP Policies
            var retryStatusCodes = new[] {
                HttpStatusCode.RequestTimeout, // 408
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout // 504
            };
            var delay = Backoff
                .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromMilliseconds(80), retryCount: 5);
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .OrResult(r => retryStatusCodes.Contains(r.StatusCode))
                .WaitAndRetryAsync(delay);
            
            // Shorter timeout for local requests
            var localTimeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(3));
            var localDelay = Backoff
                .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromMilliseconds(50), retryCount: 3);
            var localRetryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .OrResult(r => retryStatusCodes.Contains(r.StatusCode))
                .WaitAndRetryAsync(localDelay, onRetryAsync: (x, y) =>
                {
                    Debug.WriteLine("Retrying local request...");
                    return Task.CompletedTask;
                });
            
            // named client for update
            serviceCollection.AddHttpClient("UpdateClient")
                .AddPolicyHandler(retryPolicy);

            // Add Refit clients
            serviceCollection.AddRefitClient<ICivitApi>(defaultRefitSettings)
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri("https://civitai.com");
                    c.Timeout = TimeSpan.FromSeconds(15);
                })
                .AddPolicyHandler(retryPolicy);

            // Add Refit client managers
            serviceCollection.AddHttpClient("A3Client")
                .AddPolicyHandler(localTimeout.WrapAsync(localRetryPolicy));
            
            serviceCollection.AddSingleton<IA3WebApiManager>(services =>
                new A3WebApiManager(services.GetRequiredService<ISettingsManager>(),
                    services.GetRequiredService<IHttpClientFactory>())
                {
                    RefitSettings = defaultRefitSettings,
                });

            // Add logging
            serviceCollection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning)
                       .AddFilter("Microsoft", LogLevel.Warning)
                       .AddFilter("System", LogLevel.Warning);
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddNLog(logConfig);
            });

            // Remove HTTPClientFactory logging
            serviceCollection.RemoveAll<IHttpMessageHandlerBuilderFilter>();

            // Default error handling for 'SafeFireAndForget'
            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling(ex =>
            {
                Logger?.Warn(ex, "Background Task failed: {ExceptionMessage}", ex.Message);
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            var settingsManager = serviceProvider.GetRequiredService<ISettingsManager>();

            // First time setup if needed
            if (!settingsManager.IsEulaAccepted())
            {
                var setupWindow = serviceProvider.GetRequiredService<FirstLaunchSetupWindow>();
                if (setupWindow.ShowDialog() ?? false)
                {
                    settingsManager.SetEulaAccepted();
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
            var settingsManager = serviceProvider?.GetRequiredService<ISettingsManager>();

            // Skip remaining steps if no library is set
            if (!(settingsManager?.TryFindLibrary() ?? false)) return;
            
            // If RemoveFolderLinksOnShutdown is set, delete all package junctions
            if (settingsManager.Settings.RemoveFolderLinksOnShutdown)
            {
                var sharedFolders = serviceProvider?.GetRequiredService<ISharedFolders>();
                sharedFolders?.RemoveLinksForAllPackages();
            }
            
            // Dispose of database
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
                    DataContext = vm
                };

                if (MainWindow?.IsActive ?? false)
                {
                    exceptionWindow.Owner = MainWindow;
                }
                exceptionWindow.ShowDialog();
            }
            e.Handled = true;
            Current.Shutdown(1);
            Environment.Exit(1);
        }
    }
}

