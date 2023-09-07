using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Processes;
#if DEBUG
using StabilityMatrix.Avalonia.Diagnostics.Views;
#endif

namespace StabilityMatrix.Avalonia.Views;

[SuppressMessage("ReSharper", "UnusedParameter.Local")]
public partial class MainWindow : AppWindowBase
{
    private readonly INotificationService notificationService;
    private readonly INavigationService navigationService;

    private FlyoutBase? progressFlyout;

    [DesignOnly(true)]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public MainWindow()
    {
        notificationService = null!;
        navigationService = null!;
    }

    public MainWindow(
        INotificationService notificationService,
        INavigationService navigationService
    )
    {
        this.notificationService = notificationService;
        this.navigationService = navigationService;

        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
        LogWindow.Attach(this, App.Services);
#endif
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        EventManager.Instance.ToggleProgressFlyout += (_, _) => progressFlyout?.Hide();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        navigationService.SetFrame(
            FrameView ?? throw new NullReferenceException("Frame not found")
        );

        // Navigate to first page
        if (DataContext is not MainWindowViewModel vm)
        {
            throw new NullReferenceException("DataContext is not MainWindowViewModel");
        }

        navigationService.NavigateTo(vm.Pages[0], new DrillInNavigationTransitionInfo());
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
        var launchPageViewModel = App.Services.GetRequiredService<LaunchPageViewModel>();

        launchPageViewModel.OnMainWindowClosing(e);

        base.OnClosing(e);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Initialize notification service using this window as the visual root
        notificationService.Initialize(this);

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

    private void NavigationView_OnItemInvoked(object sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is NavigationViewItem nvi)
        {
            // Skip if this is the currently selected item
            if (nvi.IsSelected)
            {
                return;
            }

            if (nvi.Tag is null)
            {
                throw new InvalidOperationException("NavigationViewItem Tag is null");
            }

            if (nvi.Tag is not ViewModelBase vm)
            {
                throw new InvalidOperationException(
                    $"NavigationViewItem Tag must be of type ViewModelBase, not {nvi.Tag?.GetType()}"
                );
            }
            navigationService.NavigateTo(vm, new BetterEntranceNavigationTransition());
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
            notificationService.ShowPersistent(
                "Failed to load image",
                $"Could not load '{displayName}'\n({e.Exception.Message})",
                NotificationType.Warning
            );
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
            var color = this.TryFindResource(
                "SolidBackgroundFillColorBase",
                ThemeVariant.Dark,
                out var value
            )
                ? (Color2)(Color)value!
                : new Color2(32, 32, 32);

            color = color.LightenPercent(-0.8f);

            Background = new ImmutableSolidColorBrush(color, 0.8);
        }
        else if (ActualThemeVariant == ThemeVariant.Light)
        {
            // Similar effect here
            var color = this.TryFindResource(
                "SolidBackgroundFillColorBase",
                ThemeVariant.Light,
                out var value
            )
                ? (Color2)(Color)value!
                : new Color2(243, 243, 243);

            color = color.LightenPercent(0.5f);

            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
    }

    private void FooterDownloadItem_OnTapped(object? sender, TappedEventArgs e)
    {
        var item = sender as NavigationViewItem;
        var flyout = item!.ContextFlyout;
        flyout!.ShowAt(item);

        progressFlyout = flyout;
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
