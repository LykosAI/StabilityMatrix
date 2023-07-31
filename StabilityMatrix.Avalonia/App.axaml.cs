using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.DesignData;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;
using Application = Avalonia.Application;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace StabilityMatrix.Avalonia;

public sealed class App : Application
{
    [NotNull] public static IServiceProvider? Services { get; private set; }
    [NotNull] public static Visual? VisualRoot { get; private set; }
    [NotNull] public static IStorageProvider? StorageProvider { get; private set; }
    // ReSharper disable once MemberCanBePrivate.Global
    [NotNull] public static IConfiguration? Config { get; private set; }
    
    // ReSharper disable once MemberCanBePrivate.Global
    public IClassicDesktopStyleApplicationLifetime? DesktopLifetime =>
        ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Set design theme
        if (Design.IsDesignMode)
        {
            RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        
        if (Design.IsDesignMode)
        {
            DesignData.DesignData.Initialize();
            Services = DesignData.DesignData.Services;
        }
        else
        {
            ConfigureServiceProvider();
        }

        if (DesktopLifetime is not null)
        {
            DesktopLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // First time setup if needed
            var settingsManager = Services.GetRequiredService<ISettingsManager>();
            if (!settingsManager.IsEulaAccepted())
            {
                var setupWindow = Services.GetRequiredService<FirstLaunchSetupWindow>();
                var setupViewModel = Services.GetRequiredService<FirstLaunchSetupViewModel>();
                setupWindow.DataContext = setupViewModel;
                setupWindow.ShowAsDialog = true;
                setupWindow.ShowActivated = true;
                setupWindow.ShowAsyncCts = new CancellationTokenSource();
                
                setupWindow.ExtendClientAreaChromeHints = Program.Args.NoWindowChromeEffects ?
                    ExtendClientAreaChromeHints.NoChrome : ExtendClientAreaChromeHints.PreferSystemChrome;

                DesktopLifetime.MainWindow = setupWindow;

                setupWindow.ShowAsyncCts.Token.Register(() =>
                {
                    if (setupWindow.Result == ContentDialogResult.Primary)
                    {
                        settingsManager.SetEulaAccepted();
                        ShowMainWindow();
                        DesktopLifetime.MainWindow.Show();
                    }
                    else
                    {
                        Shutdown();
                    }
                });
            }
            else
            {
                ShowMainWindow();
            }
        }
    }

    private void ShowMainWindow()
    {
        if (DesktopLifetime is null) return;
        
        var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
        var notificationService = Services.GetRequiredService<INotificationService>();
        
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = mainViewModel;
        mainWindow.NotificationService = notificationService;
        
        mainWindow.ExtendClientAreaChromeHints = Program.Args.NoWindowChromeEffects ?
            ExtendClientAreaChromeHints.NoChrome : ExtendClientAreaChromeHints.PreferSystemChrome;

        var settingsManager = Services.GetRequiredService<ISettingsManager>();
        var windowSettings = settingsManager.Settings.WindowSettings;
        if (windowSettings != null && !Program.Args.ResetWindowPosition)
        {
            mainWindow.Position = new PixelPoint(windowSettings.X, windowSettings.Y);
            mainWindow.Width = windowSettings.Width;
            mainWindow.Height = windowSettings.Height;    
        }
        else
        {
            mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        
        mainWindow.Closing += (_, _) =>
        {
            var validWindowPosition =
                mainWindow.Screens.All.Any(screen => screen.Bounds.Contains(mainWindow.Position));

            settingsManager.Transaction(s =>
            {
                s.WindowSettings = new WindowSettings(
                    mainWindow.Width, mainWindow.Height,
                    validWindowPosition ? mainWindow.Position.X : 0,
                    validWindowPosition ? mainWindow.Position.Y : 0);
            }, ignoreMissingLibraryDir: true);
        };
        mainWindow.Closed += (_, _) => Shutdown();

        VisualRoot = mainWindow;
        StorageProvider = mainWindow.StorageProvider;
            
        DesktopLifetime.MainWindow = mainWindow;
        DesktopLifetime.Exit += OnExit;
    }

    private static void ConfigureServiceProvider()
    {
        var services = ConfigureServices();
        Services = services.BuildServiceProvider();
        
        var settingsManager = Services.GetRequiredService<ISettingsManager>();
        settingsManager.TryFindLibrary();
        Services.GetRequiredService<ProgressManagerViewModel>().StartEventListener();
    }

    internal static void ConfigurePageViewModels(IServiceCollection services)
    {
        services.AddSingleton<PackageManagerViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<CheckpointBrowserViewModel>()
            .AddSingleton<CheckpointBrowserCardViewModel>()
            .AddSingleton<CheckpointsPageViewModel>()
            .AddSingleton<LaunchPageViewModel>()
            .AddSingleton<ProgressManagerViewModel>()
            .AddSingleton<InferenceViewModel>();
        
        services.AddSingleton<MainWindowViewModel>(provider =>
            new MainWindowViewModel(provider.GetRequiredService<ISettingsManager>(),
                provider.GetRequiredService<ServiceManager<ViewModelBase>>())
            {
                Pages =
                {
                    provider.GetRequiredService<LaunchPageViewModel>(),
                    provider.GetRequiredService<InferenceViewModel>(),
                    provider.GetRequiredService<PackageManagerViewModel>(),
                    provider.GetRequiredService<CheckpointsPageViewModel>(),
                    provider.GetRequiredService<CheckpointBrowserViewModel>(),
                },
                FooterPages =
                {
                    provider.GetRequiredService<SettingsViewModel>()
                }
            });
        
        // Register disposable view models for shutdown cleanup
        services.AddSingleton<IDisposable>(p 
            => p.GetRequiredService<LaunchPageViewModel>());
    }

    internal static void ConfigureDialogViewModels(IServiceCollection services)
    {
        // Dialog view models (transient)
        services.AddTransient<InstallerViewModel>();
        services.AddTransient<OneClickInstallViewModel>();
        services.AddTransient<SelectModelVersionViewModel>();
        services.AddTransient<SelectDataDirectoryViewModel>();
        services.AddTransient<LaunchOptionsViewModel>();
        services.AddTransient<ExceptionViewModel>();
        services.AddTransient<EnvVarsViewModel>();
        services.AddSingleton<FirstLaunchSetupViewModel>();
        services.AddSingleton<UpdateViewModel>();
        
        // Other transients (usually sub view models)
        services.AddTransient<CheckpointFolder>();
        services.AddTransient<CheckpointFile>();
        services.AddTransient<InferenceTextToImageViewModel>();
        
        // Global progress
        services.AddSingleton<ProgressManagerViewModel>();
        
        // Controls
        services.AddTransient<RefreshBadgeViewModel>();
        
        // Dialog factory
        services.AddSingleton<ServiceManager<ViewModelBase>>(provider =>
            new ServiceManager<ViewModelBase>()
                .Register(provider.GetRequiredService<InstallerViewModel>)
                .Register(provider.GetRequiredService<OneClickInstallViewModel>)
                .Register(provider.GetRequiredService<SelectModelVersionViewModel>)
                .Register(provider.GetRequiredService<SelectDataDirectoryViewModel>)
                .Register(provider.GetRequiredService<LaunchOptionsViewModel>)
                .Register(provider.GetRequiredService<UpdateViewModel>)
                .Register(provider.GetRequiredService<CheckpointFolder>)
                .Register(provider.GetRequiredService<CheckpointFile>)
                .Register(provider.GetRequiredService<RefreshBadgeViewModel>)
                .Register(provider.GetRequiredService<ExceptionViewModel>)
                .Register(provider.GetRequiredService<EnvVarsViewModel>)
                .Register(provider.GetRequiredService<ProgressManagerViewModel>)
                .Register(provider.GetRequiredService<InferenceTextToImageViewModel>)
                .Register(provider.GetRequiredService<FirstLaunchSetupViewModel>));
    }

    internal static void ConfigureViews(IServiceCollection services)
    {
        // Pages
        services.AddSingleton<CheckpointsPage>();
        services.AddSingleton<LaunchPageView>();
        services.AddSingleton<PackageManagerPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<CheckpointBrowserPage>();
        services.AddSingleton<ProgressManagerPage>();
        services.AddSingleton<InferencePage>();
        
        // Inference tabs
        services.AddTransient<InferenceTextToImageView>();
        
        // Dialogs
        services.AddTransient<SelectDataDirectoryDialog>();
        services.AddTransient<LaunchOptionsDialog>();
        services.AddTransient<UpdateDialog>();
        services.AddTransient<ExceptionDialog>();
        services.AddTransient<EnvVarsDialog>();
        
        // Controls
        services.AddTransient<RefreshBadge>();
        
        // Windows
        services.AddSingleton<MainWindow>();
        services.AddSingleton<FirstLaunchSetupWindow>();
    }
    
    internal static void ConfigurePackages(IServiceCollection services)
    {
        services.AddSingleton<BasePackage, A3WebUI>();
        services.AddSingleton<BasePackage, VladAutomatic>();
        services.AddSingleton<BasePackage, ComfyUI>();
        services.AddSingleton<BasePackage, VoltaML>();
        services.AddSingleton<BasePackage, InvokeAI>();
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddMemoryCache();

        ConfigurePageViewModels(services);
        ConfigureDialogViewModels(services);
        ConfigurePackages(services);
        
        // Other services
        services.AddSingleton<ISettingsManager, SettingsManager>();
        services.AddSingleton<ISharedFolders, SharedFolders>();
        services.AddSingleton<SharedState>();
        services.AddSingleton<ModelFinder>();
        services.AddSingleton<IPackageFactory, PackageFactory>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IGithubApiCache, GithubApiCache>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPyRunner, PyRunner>();
        services.AddSingleton<IUpdateHelper, UpdateHelper>();

        Config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        
        services.Configure<DebugOptions>(Config.GetSection(nameof(DebugOptions)));
        
        if (Compat.IsWindows)
        {
            services.AddSingleton<IPrerequisiteHelper, WindowsPrerequisiteHelper>();
        }
        else if (Compat.IsLinux || Compat.IsMacOS)
        {
            services.AddSingleton<IPrerequisiteHelper, UnixPrerequisiteHelper>();
        }

        ConfigureViews(services);

        if (Design.IsDesignMode)
        {
            services.AddSingleton<ILiteDbContext, MockLiteDbContext>();
        }
        else
        {
            services.AddSingleton<ILiteDbContext, LiteDbContext>();
            services.AddSingleton<IDisposable>(p => p.GetRequiredService<ILiteDbContext>());
        }

        services.AddTransient<IGitHubClient, GitHubClient>(_ =>
        {
            var client = new GitHubClient(new ProductHeaderValue("StabilityMatrix"));
            // var githubApiKey = Config["GithubApiKey"];
            // if (string.IsNullOrWhiteSpace(githubApiKey))
            //     return client;
            //
            // client.Credentials = new Credentials(githubApiKey);
            return client;
        });

        // Configure Refit and Polly
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        jsonSerializerOptions.Converters.Add(new ObjectToInferredTypesConverter());
        jsonSerializerOptions.Converters.Add(new DefaultUnknownEnumConverter<CivitFileType>());
        jsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        var defaultRefitSettings = new RefitSettings
        {
            ContentSerializer =
                new SystemTextJsonContentSerializer(jsonSerializerOptions)
        };

        // HTTP Policies
        var retryStatusCodes = new[]
        {
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout // 504
        };
        var delay = Backoff
            .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromMilliseconds(80),
                retryCount: 5);
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .OrResult(r => retryStatusCodes.Contains(r.StatusCode))
            .WaitAndRetryAsync(delay);

        // Shorter timeout for local requests
        var localTimeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(3));
        var localDelay = Backoff
            .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromMilliseconds(50),
                retryCount: 3);
        var localRetryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .OrResult(r => retryStatusCodes.Contains(r.StatusCode))
            .WaitAndRetryAsync(localDelay, onRetryAsync: (_, _) =>
            {
                Debug.WriteLine("Retrying local request...");
                return Task.CompletedTask;
            });

        // named client for update
        services.AddHttpClient("UpdateClient")
            .AddPolicyHandler(retryPolicy);

        // Add Refit clients
        services.AddRefitClient<ICivitApi>(defaultRefitSettings)
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri("https://civitai.com");
                c.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddPolicyHandler(retryPolicy);

        // Add Refit client managers
        services.AddHttpClient("A3Client")
            .AddPolicyHandler(localTimeout.WrapAsync(localRetryPolicy));

        // Add Refit client factory
        services.AddSingleton<IApiFactory, ApiFactory>(provider =>
            new ApiFactory(provider.GetRequiredService<IHttpClientFactory>())
            {
                RefitSettings = defaultRefitSettings,
            });
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning)
                .AddFilter("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogLevel.Warning)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning);
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddNLog(ConfigureLogging());
        });

        return services;
    }

    /// <summary>
    /// Requests shutdown of the Current Application.
    /// </summary>
    /// <remarks>This returns asynchronously *without waiting* for Shutdown</remarks>
    /// <param name="exitCode">Exit code for the application.</param>
    /// <exception cref="NullReferenceException">If Application.Current is null</exception>
    public static void Shutdown(int exitCode = 0)
    {
        if (Current is null) throw new NullReferenceException(
            "Current Application was null when Shutdown called");
        if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown(exitCode);
        }
    }

    private static void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
    {
        Debug.WriteLine("Start OnExit");
        // Services.GetRequiredService<LaunchViewModel>().OnShutdown();
        var settingsManager = Services.GetRequiredService<ISettingsManager>();

        // If RemoveFolderLinksOnShutdown is set, delete all package junctions
        if (settingsManager is
            {
                IsLibraryDirSet: true,
                Settings.RemoveFolderLinksOnShutdown: true
            })
        {
            var sharedFolders = Services.GetRequiredService<ISharedFolders>();
            sharedFolders.RemoveLinksForAllPackages();
        }

        Debug.WriteLine("Start OnExit: Disposing services");
        // Dispose all services
        foreach (var disposable in Services.GetServices<IDisposable>())
        {
            Debug.WriteLine($"Disposing {disposable.GetType().Name}");
            disposable.Dispose();
        }

        Debug.WriteLine("End OnExit");
    }

    private static LoggingConfiguration ConfigureLogging()
    {
        LogManager.Setup().LoadConfiguration(builder => {
            var debugTarget = builder.ForTarget("console").WriteTo(new DebuggerTarget
            {
                Layout = "${message}"
            }).WithAsync();
            
            var fileTarget = builder.ForTarget("logfile").WriteTo(new FileTarget
            {
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}",
                ArchiveOldFileOnStartup = true,
                FileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.log",
                ArchiveFileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.{#}.log",
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 2
            }).WithAsync();
            
            // Filter some sources to be warn levels or above only
            builder.ForLogger("System.*").WriteToNil(NLog.LogLevel.Warn);
            builder.ForLogger("Microsoft.*").WriteToNil(NLog.LogLevel.Warn);
            builder.ForLogger("Microsoft.Extensions.Http.*").WriteToNil(NLog.LogLevel.Warn);
            
            builder.ForLogger().FilterMinLevel(NLog.LogLevel.Trace).WriteTo(debugTarget);
            builder.ForLogger().FilterMinLevel(NLog.LogLevel.Debug).WriteTo(fileTarget);
        });
        
        // Sentry
        if (SentrySdk.IsEnabled)
        {
            LogManager.Configuration.AddSentry(o =>
            {
                o.InitializeSdk = false;
                o.Layout = "${message}";
                o.ShutdownTimeoutSeconds = 5;
                o.IncludeEventDataOnBreadcrumbs = true;
                o.BreadcrumbLayout = "${logger}: ${message}";
                // Debug and higher are stored as breadcrumbs (default is Info)
                o.MinimumBreadcrumbLevel = NLog.LogLevel.Debug;
                // Error and higher is sent as event (default is Error)
                o.MinimumEventLevel = NLog.LogLevel.Error;
            });
        }

        return LogManager.Configuration;
    }
}
