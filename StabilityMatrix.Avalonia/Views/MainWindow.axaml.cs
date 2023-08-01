using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Views;

[SuppressMessage("ReSharper", "UnusedParameter.Local")]
public partial class MainWindow : AppWindowBase
{
    public INotificationService? NotificationService { get; set; }
    
    public MainWindow()
    {
        InitializeComponent();
        
#if DEBUG
        this.AttachDevTools();
#endif
        
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Application.Current!.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        
        var theme = ActualThemeVariant;
        // Enable mica for Windows 11
        if (IsWindows11 && theme != FluentAvaloniaTheme.HighContrastTheme)
        {
            TryEnableMicaEffect();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Show confirmation if package running
        var launchPageViewModel = App.Services
            .GetRequiredService<LaunchPageViewModel>();

        launchPageViewModel.OnMainWindowClosing(e);
        
        base.OnClosing(e);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Initialize notification service using this window as the visual root
        NotificationService?.Initialize(this);
        
        // Attach error notification handler for image loader
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.LoadFailed += OnImageLoadFailed;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        // Detach error notification handler for image loader
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.LoadFailed -= OnImageLoadFailed;
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (IsWindows11)
        {
            if (ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
            {
                TryEnableMicaEffect();
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(TransparencyBackgroundFallbackProperty);
            }
        }
    }
    
    private void OnImageLoadFailed(object? sender, ImageLoadFailedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var fileName = Path.GetFileName(e.Url);
            var displayName = string.IsNullOrEmpty(fileName) ? e.Url : fileName;
            NotificationService?.ShowPersistent(
                "Failed to load image",
                $"Could not load '{displayName}'\n({e.Exception.Message})",
                NotificationType.Warning);
        });
    }
    
    private void TryEnableMicaEffect()
    {
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = new[]
        {
            WindowTransparencyLevel.Mica, 
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur
        };
        
        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            var color = this.TryFindResource("SolidBackgroundFillColorBase",
                ThemeVariant.Dark, out var value) ? (Color2)(Color)value! : new Color2(32, 32, 32);

            color = color.LightenPercent(-0.8f);

            Background = new ImmutableSolidColorBrush(color, 0.8);
        }
        else if (ActualThemeVariant == ThemeVariant.Light)
        {
            // Similar effect here
            var color = this.TryFindResource("SolidBackgroundFillColorBase",
                ThemeVariant.Light, out var value) ? (Color2)(Color)value! : new Color2(243, 243, 243);

            color = color.LightenPercent(0.5f);

            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
    }

    private void FooterDownloadItem_OnTapped(object? sender, TappedEventArgs e)
    {
        var item = sender as NavigationViewItem;
        var flyout = item!.ContextFlyout;
        flyout!.ShowAt(item);
    }

    private void FooterUpdateItem_OnTapped(object? sender, TappedEventArgs e)
    {
        // show update window thing
        if (DataContext is not MainWindowViewModel vm)
        {
            throw new NullReferenceException("DataContext is not MainWindowViewModel");
        }
        Dispatcher.UIThread.InvokeAsync(vm.ShowUpdateDialog).SafeFireAndForget();
    }

    private void FooterDiscordItem_OnTapped(object? sender, TappedEventArgs e)
    {
        ProcessRunner.OpenUrl(Assets.DiscordServerUrl);
    }

    private void PatreonPatreonItem_OnTapped(object? sender, TappedEventArgs e)
    {
        ProcessRunner.OpenUrl(Assets.PatreonUrl);
    }
}
