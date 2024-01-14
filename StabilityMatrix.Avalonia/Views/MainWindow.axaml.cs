using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
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
using FluentAvalonia.Interop;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using ILogger = NLog.ILogger;
using TeachingTip = FluentAvalonia.UI.Controls.TeachingTip;
#if DEBUG
using StabilityMatrix.Avalonia.Diagnostics.Views;
#endif

namespace StabilityMatrix.Avalonia.Views;

[SuppressMessage("ReSharper", "UnusedParameter.Local")]
[Singleton]
public partial class MainWindow : AppWindowBase
{
    private readonly INotificationService notificationService;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly ISettingsManager settingsManager;
    private readonly ILogger<MainWindow> logger;

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
        INavigationService<MainWindowViewModel> navigationService,
        ISettingsManager settingsManager,
        ILogger<MainWindow> logger
    )
    {
        this.notificationService = notificationService;
        this.navigationService = navigationService;
        this.settingsManager = settingsManager;
        this.logger = logger;

        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
        this.AttachDebugSaveScreenshot();
        LogWindow.Attach(this, App.Services);
#endif
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        navigationService.TypedNavigation += NavigationService_OnTypedNavigation;

        EventManager.Instance.ToggleProgressFlyout += (_, _) => progressFlyout?.Hide();
        EventManager.Instance.CultureChanged += (_, _) => SetDefaultFonts();
        EventManager.Instance.UpdateAvailable += OnUpdateAvailable;
        EventManager.Instance.NavigateAndFindCivitModelRequested += OnNavigateAndFindCivitModelRequested;

        Observable
            .FromEventPattern<SizeChangedEventArgs>(this, nameof(SizeChanged))
            .Where(x => x.EventArgs.PreviousSize != x.EventArgs.NewSize)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => x.EventArgs.NewSize)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(newSize =>
            {
                var validWindowPosition = Screens.All.Any(screen => screen.Bounds.Contains(Position));

                settingsManager.Transaction(
                    s =>
                    {
                        s.WindowSettings = new WindowSettings(
                            newSize.Width,
                            newSize.Height,
                            validWindowPosition ? Position.X : 0,
                            validWindowPosition ? Position.Y : 0
                        );
                    },
                    ignoreMissingLibraryDir: true
                );
            });

        Observable
            .FromEventPattern<PixelPointEventArgs>(this, nameof(PositionChanged))
            .Where(x => Screens.All.Any(screen => screen.Bounds.Contains(x.EventArgs.Point)))
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => x.EventArgs.Point)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(position =>
            {
                settingsManager.Transaction(
                    s =>
                    {
                        s.WindowSettings = new WindowSettings(Width, Height, position.X, position.Y);
                    },
                    ignoreMissingLibraryDir: true
                );
            });
    }

    private void OnNavigateAndFindCivitModelRequested(object? sender, int e)
    {
        navigationService.NavigateTo<CheckpointBrowserViewModel>();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        navigationService.SetFrame(FrameView ?? throw new NullReferenceException("Frame not found"));
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

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        App.Shutdown();
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

        if (DataContext is not MainWindowViewModel vm)
            return;

        // Navigate to first page
        Dispatcher.UIThread.Post(
            () =>
                navigationService.NavigateTo(
                    vm.Pages[0],
                    new BetterSlideNavigationTransition
                    {
                        Effect = SlideNavigationTransitionEffect.FromBottom
                    }
                )
        );

        // Check show update teaching tip
        if (vm.UpdateViewModel.IsUpdateAvailable)
        {
            OnUpdateAvailable(this, vm.UpdateViewModel.UpdateInfo);
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

    private void NavigationService_OnTypedNavigation(object? sender, TypedNavigationEventArgs e)
    {
        var mainViewModel = (MainWindowViewModel)DataContext!;

        mainViewModel.SelectedCategory = mainViewModel
            .Pages.Concat(mainViewModel.FooterPages)
            .FirstOrDefault(x => x.GetType() == e.ViewModelType);
    }

    private void OnUpdateAvailable(object? sender, UpdateInfo? updateInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm && vm.ShouldShowUpdateAvailableTeachingTip(updateInfo))
            {
                var target = this.FindControl<NavigationViewItem>("FooterUpdateItem")!;
                var tip = this.FindControl<TeachingTip>("UpdateAvailableTeachingTip")!;

                tip.Target = target;
                tip.Subtitle = $"{Compat.AppVersion.ToDisplayString()} -> {updateInfo.Version}";
                tip.IsOpen = true;
            }
        });
    }

    public void SetDefaultFonts()
    {
        var fonts = new List<string>();

        try
        {
            if (Cultures.Current?.Name == "ja-JP")
            {
                var customFont = (Application.Current!.Resources["NotoSansJP"] as FontFamily)!;
                Resources["ContentControlThemeFontFamily"] = customFont;
                FontFamily = customFont;
                return;
            }

            if (Compat.IsWindows)
            {
                if (OSVersionHelper.IsWindows11())
                {
                    fonts.Add("Segoe UI Variable Text");
                }
                else
                {
                    fonts.Add("Segoe UI");
                }
            }
            else if (Compat.IsMacOS)
            {
                fonts.Add("San Francisco");
                fonts.Add("Helvetica Neue");
                fonts.Add("Helvetica");
            }
            else
            {
                Resources["ContentControlThemeFontFamily"] = FontFamily.Default;
                FontFamily = FontFamily.Default;
                return;
            }

            var fontString = new FontFamily(string.Join(",", fonts));
            Resources["ContentControlThemeFontFamily"] = fontString;
            FontFamily = fontString;
        }
        catch (Exception e)
        {
            LogManager.GetCurrentClassLogger().Error(e);

            Resources["ContentControlThemeFontFamily"] = FontFamily.Default;
            FontFamily = FontFamily.Default;
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
        var fileName = Path.GetFileName(e.Url);
        var displayName = string.IsNullOrEmpty(fileName) ? e.Url : fileName;
        logger.LogWarning($"Could not load '{displayName}'\n({e.Exception.Message})");
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
            var color = this.TryFindResource("SolidBackgroundFillColorBase", ThemeVariant.Dark, out var value)
                ? (Color2)(Color)value!
                : new Color2(30, 31, 34);

            color = color.LightenPercent(-0.5f);

            Background = new ImmutableSolidColorBrush(color, 0.72);
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

    private async void FooterUpdateItem_OnTapped(object? sender, TappedEventArgs e)
    {
        // show update window thing
        if (DataContext is not MainWindowViewModel vm)
        {
            throw new NullReferenceException("DataContext is not MainWindowViewModel");
        }

        await vm.ShowUpdateDialog();
    }

    private void FooterDiscordItem_OnTapped(object? sender, TappedEventArgs e)
    {
        ProcessRunner.OpenUrl(Assets.DiscordServerUrl);
    }

    private void PatreonPatreonItem_OnTapped(object? sender, TappedEventArgs e)
    {
        ProcessRunner.OpenUrl(Assets.PatreonUrl);
    }

    private void TopLevel_OnBackRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        navigationService.GoBack();
    }
}
