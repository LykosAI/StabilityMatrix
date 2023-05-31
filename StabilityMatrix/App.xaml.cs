using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;
using StabilityMatrix.Python;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Services;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? serviceProvider;
        
        public static IConfiguration Config { get; set; }

        public App()
        {
            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // Configure window exception handler when not in debug mode
            if (!Debugger.IsAttached)
            {
                DispatcherUnhandledException += App_DispatcherUnhandledException;
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddSingleton<IContentDialogService, ContentDialogService>();
            serviceCollection.AddSingleton<PageContentDialogService>();
            serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
            serviceCollection.AddSingleton<IPackageFactory, PackageFactory>();
            serviceCollection.AddSingleton<IPyRunner, PyRunner>();
            serviceCollection.AddTransient<IDialogFactory, DialogFactory>();
            
            serviceCollection.AddTransient<MainWindow>();
            serviceCollection.AddTransient<SettingsPage>();
            serviceCollection.AddTransient<LaunchPage>();
            serviceCollection.AddTransient<PackageManagerPage>();
            serviceCollection.AddTransient<TextToImagePage>();
            serviceCollection.AddTransient<InstallerWindow>();
            
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<SnackbarViewModel>();
            serviceCollection.AddTransient<LaunchOptionsDialogViewModel>();
            serviceCollection.AddSingleton<SettingsViewModel>();
            serviceCollection.AddSingleton<LaunchViewModel>();
            serviceCollection.AddSingleton<PackageManagerViewModel>();
            serviceCollection.AddSingleton<TextToImageViewModel>();
            serviceCollection.AddTransient<InstallerViewModel>();
            
            serviceCollection.AddSingleton<BasePackage, A3WebUI>();
            serviceCollection.AddSingleton<BasePackage, VladAutomatic>();
            serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
            serviceCollection.AddSingleton<ISettingsManager, SettingsManager>();
            serviceCollection.AddSingleton<IDialogErrorHandler, DialogErrorHandler>();
            serviceCollection.AddMemoryCache();
            serviceCollection.AddSingleton<IGithubApiCache, GithubApiCache>();

            var defaultRefitSettings = new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                })
            };
            
            // Polly retry policy for Refit
            var delay = Backoff
                .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: 5);
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(delay);

            // Add Refit clients
            serviceCollection.AddRefitClient<IGithubApi>(defaultRefitSettings)
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri("https://api.github.com");
                    c.Timeout = TimeSpan.FromSeconds(5);

                    var githubApiKey = Config["GithubApiKey"];
                    if (!string.IsNullOrEmpty(githubApiKey))
                    {
                        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubApiKey}");
                    }
                })
                .AddPolicyHandler(retryPolicy);
            serviceCollection.AddRefitClient<IA3WebApi>(defaultRefitSettings)
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri("http://localhost:7860");
                    c.Timeout = TimeSpan.FromSeconds(2);
                })
                .AddPolicyHandler(retryPolicy);

            // Logging configuration
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
            var logConfig = new NLog.Config.LoggingConfiguration();
            // File logging
            var fileTarget = new NLog.Targets.FileTarget("logfile") { FileName = logPath };
            // Log trace+ to debug console
            var debugTarget = new NLog.Targets.DebuggerTarget("debugger") { Layout = "${message}" };
            logConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, fileTarget);
            logConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, debugTarget);
            NLog.LogManager.Configuration = logConfig;

            serviceCollection.AddLogging(log =>
            {
                log.ClearProviders();
                log.SetMinimumLevel(LogLevel.Trace);
                log.AddNLog(logConfig);
            });

            serviceProvider = serviceCollection.BuildServiceProvider();
            var window = serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            serviceProvider?.GetRequiredService<LaunchViewModel>().OnShutdown();
        }
        
        [DoesNotReturn]
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
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
            e.Handled = true;
            Environment.Exit(1);
        }
    }
}

