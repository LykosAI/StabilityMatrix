using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Packages;
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
        if (Design.IsDesignMode)
        {
            DesignData.DesignData.Initialize();
            Services = DesignData.DesignData.Services;
        }
        else
        {
            ConfigureServiceProvider();
        }
        
        var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
        var notificationService = Services.GetRequiredService<INotificationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = mainViewModel;
            mainWindow.NotificationService = notificationService;

            VisualRoot = mainWindow;
            StorageProvider = mainWindow.StorageProvider;
            
            desktop.MainWindow = mainWindow;
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
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
            .AddSingleton<ProgressManagerViewModel>();
        
        services.AddSingleton<MainWindowViewModel>(provider =>
            new MainWindowViewModel(provider.GetRequiredService<ISettingsManager>(),
                provider.GetRequiredService<ServiceManager<ViewModelBase>>())
            {
                Pages =
                {
                    provider.GetRequiredService<LaunchPageViewModel>(),
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
        services.AddSingleton<UpdateViewModel>();
        
        // Other transients (usually sub view models)
        services.AddTransient<CheckpointFolder>();
        services.AddTransient<CheckpointFile>();
        
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
                .Register(provider.GetRequiredService<ProgressManagerViewModel>));
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
        
        // Dialogs
        services.AddTransient<SelectDataDirectoryDialog>();
        services.AddTransient<LaunchOptionsDialog>();
        services.AddTransient<UpdateDialog>();
        services.AddTransient<ExceptionDialog>();
        
        // Controls
        services.AddTransient<RefreshBadge>();
        
        // Window
        services.AddSingleton<MainWindow>();
    }
    
    internal static void ConfigurePackages(IServiceCollection services)
    {
        services.AddSingleton<BasePackage, A3WebUI>();
        services.AddSingleton<BasePackage, VladAutomatic>();
        services.AddSingleton<BasePackage, ComfyUI>();
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
        services.AddSingleton<ModelFinder>();
        services.AddSingleton<IPackageFactory, PackageFactory>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IGithubApiCache, GithubApiCache>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPyRunner, PyRunner>();
        services.AddSingleton<IUpdateHelper, UpdateHelper>();

        if (Compat.IsWindows)
        {
            services.AddSingleton<IPrerequisiteHelper, WindowsPrerequisiteHelper>();
        }
        else if (Compat.IsLinux)
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

    public static void Shutdown()
    {
        if (Current is null) throw new InvalidOperationException("Current Application is not defined");
        if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
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
        var logConfig = new LoggingConfiguration();

        // File target
        logConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, 
            new FileTarget("logfile")
            {
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}",
                ArchiveOldFileOnStartup = true,
                FileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.log",
                ArchiveFileName = "${specialfolder:folder=ApplicationData}/StabilityMatrix/app.{#}.log",
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 2
            });
        
        // Debugger Target
        logConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, 
            new DebuggerTarget("debugger")
            {
                Layout = "${message}"
            });
        
        // Sentry
        if (SentrySdk.IsEnabled)
        {
            logConfig.AddSentry(o =>
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

        LogManager.Configuration = logConfig;


        return logConfig;
    }
}
