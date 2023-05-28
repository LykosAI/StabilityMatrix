using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Windows.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;
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

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // Configure window exception handler when not in debug mode
            if (!Debugger.IsAttached)
            {
                DispatcherUnhandledException += App_DispatcherUnhandledException;
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddTransient<MainWindow>();
            serviceCollection.AddTransient<SettingsPage>();
            serviceCollection.AddTransient<LaunchPage>();
            serviceCollection.AddTransient<InstallPage>();
            serviceCollection.AddTransient<TextToImagePage>();
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddSingleton<SettingsViewModel>();
            serviceCollection.AddSingleton<LaunchViewModel>();
            serviceCollection.AddSingleton<InstallerViewModel>();
            serviceCollection.AddSingleton<TextToImageViewModel>();
            serviceCollection.AddSingleton<LaunchOptionsDialogViewModel>();
            serviceCollection.AddSingleton<IContentDialogService, ContentDialogService>();
            serviceCollection.AddSingleton<IPackageFactory, PackageFactory>();
            serviceCollection.AddSingleton<BasePackage, A3WebUI>();
            serviceCollection.AddSingleton<BasePackage, DankDiffusion>();
            serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
            serviceCollection.AddSingleton<ISettingsManager, SettingsManager>();
            
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            var refitSettings = new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions)
            };
            
            serviceCollection.AddRefitClient<IGithubApi>();
            serviceCollection.AddRefitClient<IA3WebApi>(refitSettings).ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("http://localhost:7860");
            });

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
