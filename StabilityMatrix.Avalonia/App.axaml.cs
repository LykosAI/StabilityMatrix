using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.Views;

namespace StabilityMatrix.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;
    
    
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
        ConfigureServiceProvider();
        
        var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private static void ConfigureServiceProvider()
    {
        var services = ConfigureServices();
        Services = services.BuildServiceProvider();
    }
    
    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();
        
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<LaunchPageViewModel>();
        services.AddSingleton<PackageManagerViewModel>();
        
        services.AddTransient<LaunchPageView>();
        services.AddTransient<PackageManagerPage>();
        
        return services;
    }
}
