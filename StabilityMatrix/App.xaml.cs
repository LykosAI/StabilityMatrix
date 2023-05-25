using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddTransient<MainWindow>();
            serviceCollection.AddTransient<SettingsPage>();
            serviceCollection.AddTransient<LaunchPage>();
            serviceCollection.AddTransient<InstallPage>();
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddSingleton<SettingsViewModel>();
            serviceCollection.AddSingleton<ISettingsManager, SettingsManager>();
            serviceCollection.AddRefitClient<IGithubApi>();

            var provider = serviceCollection.BuildServiceProvider();
            var window = provider.GetRequiredService<MainWindow>();
            window.Show();
        }
    }
}
