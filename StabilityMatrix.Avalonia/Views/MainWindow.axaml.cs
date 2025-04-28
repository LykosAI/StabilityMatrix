using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Windowing;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using TeachingTip = FluentAvalonia.UI.Controls.TeachingTip;
#if SM_LOG_WINDOW
using StabilityMatrix.Avalonia.Diagnostics.Views;
using StabilityMatrix.Avalonia.Extensions;
#endif

namespace StabilityMatrix.Avalonia.Views;

[SuppressMessage("ReSharper", "UnusedParameter.Local")]
[RegisterSingleton<MainWindow>]
public partial class MainWindow : AppWindowBase
{
    private readonly INotificationService notificationService;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly ISettingsManager settingsManager;
    private readonly ILogger<MainWindow> logger;

    private FlyoutBase? progressFlyout;

    /*[DesignOnly(true)]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public MainWindow()
        : this(
            DesignData.DesignData.Services.GetRequiredService<INotificationService>(),
            DesignData.DesignData.Services.GetRequiredService<INavigationService<MainWindowViewModel>>(),
            DesignData.DesignData.Services.GetRequiredService<ISettingsManager>(),
            DesignData.DesignData.Services.GetRequiredService<ILogger<MainWindow>>()
        )
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("Design constructor called in non-design mode");
        }
    }*/

    public MainWindow(
        INotificationService notificationService,
        INavigationService<MainWindowViewModel> navigationService,
        ISettingsManager settingsManager,
        ILogger<MainWindow> logger,
        Lazy<MainWindowViewModel> lazyViewModel
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
#endif

#if SM_LOG_WINDOW
        LogWindow.Attach(this, App.Services);
#endif
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        ExtendClientAreaChromeHints = Program.Args.NoWindowChromeEffects
            ? ExtendClientAreaChromeHints.NoChrome
            : ExtendClientAreaChromeHints.PreferSystemChrome;

        // Load window positions
        if (
            settingsManager.Settings.WindowSettings is { } windowSettings
            && !Program.Args.ResetWindowPosition
        )
        {
            Position = new PixelPoint(windowSettings.X, windowSettings.Y);
            Width = Math.Max(300, windowSettings.Width);
            Height = Math.Max(300, windowSettings.Height);
            WindowState = windowSettings.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (Program.Args.IsSplashScreenEnabled)
        {
            var appIconStream = Assets.AppIcon.Open();
            var appIcon = new Bitmap(appIconStream);
            appIconStream.Dispose();

            SplashScreen = new ApplicationSplashScreen
            {
                AppIcon = appIcon,
                InitApp = cancellationToken =>
                {
                    return Dispatcher
                        .UIThread.InvokeAsync(() => StartupInitialize(lazyViewModel, cancellationToken))
                        .GetTask();
                },
            };
        }
        else
        {
            StartupInitialize(lazyViewModel);
        }
    }

    /// <summary>
    /// Run startup initialization.
    /// This runs on the UI thread.
    /// </summary>
    private void StartupInitialize(
        Lazy<MainWindowViewModel> lazyViewModel,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = CodeTimer.StartDebug();

        Dispatcher.UIThread.VerifyAccess();

        cancellationToken.ThrowIfCancellationRequested();

        navigationService.TypedNavigation += NavigationService_OnTypedNavigation;

        EventManager.Instance.ToggleProgressFlyout += (_, _) => progressFlyout?.Hide();
        EventManager.Instance.CultureChanged += (_, _) => SetDefaultFonts();
        EventManager.Instance.UpdateAvailable += OnUpdateAvailable;
        EventManager.Instance.NavigateAndFindCivitModelRequested += OnNavigateAndFindCivitModelRequested;
        EventManager.Instance.DownloadsTeachingTipRequested += InstanceOnDownloadsTeachingTipRequested;

        SetDefaultFonts();

        Observable
            .FromEventPattern<SizeChangedEventArgs>(this, nameof(SizeChanged))
            .Where(x => x.EventArgs.PreviousSize != x.EventArgs.NewSize)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => x.EventArgs.NewSize)
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(newSize =>
            {
                var validWindowPosition = Screens.All.Any(screen => screen.Bounds.Contains(Position));

                settingsManager.Transaction(
                    s =>
                    {
                        var isMaximized = WindowState == WindowState.Maximized;
                        if (isMaximized && s.WindowSettings != null)
                        {
                            s.WindowSettings = s.WindowSettings with { IsMaximized = true };
                        }
                        else
                        {
                            s.WindowSettings = new WindowSettings(
                                newSize.Width,
                                newSize.Height,
                                validWindowPosition ? Position.X : 0,
                                validWindowPosition ? Position.Y : 0,
                                WindowState == WindowState.Maximized
                            );
                        }
                    },
                    ignoreMissingLibraryDir: true
                );
            });

        Observable
            .FromEventPattern<PixelPointEventArgs>(this, nameof(PositionChanged))
            .Where(x => Screens.All.Any(screen => screen.Bounds.Contains(x.EventArgs.Point)))
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => x.EventArgs.Point)
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(position =>
            {
                settingsManager.Transaction(
                    s =>
                    {
                        var isMaximized = WindowState == WindowState.Maximized;
                        var validWindowPosition = Screens.All.Any(screen => screen.Bounds.Contains(position));

                        if (isMaximized && s.WindowSettings != null)
                        {
                            s.WindowSettings = s.WindowSettings with { IsMaximized = true };
                        }
                        else
                        {
                            s.WindowSettings = new WindowSettings(
                                Width,
                                Height,
                                validWindowPosition ? position.X : 0,
                                validWindowPosition ? position.Y : 0,
                                WindowState == WindowState.Maximized
                            );
                        }
                    },
                    ignoreMissingLibraryDir: true
                );
            });

        using (CodeTimer.StartDebug("Load view model"))
        {
            var viewModel = lazyViewModel.Value;
            DataContext = viewModel;
        }
    }

    private void InstanceOnDownloadsTeachingTipRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (
                !settingsManager.Settings.SeenTeachingTips.Contains(
                    Core.Models.Settings.TeachingTip.DownloadsTip
                )
            )
            {
                var target = this.FindControl<NavigationViewItem>("FooterDownloadItem")!;
                var tip = this.FindControl<TeachingTip>("DownloadsTeachingTip")!;

                tip.Target = target;
                tip.Subtitle = Languages.Resources.TeachingTip_DownloadsExplanation;
                tip.IsOpen = true;
            }
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
        var runningPackageService = App.Services.GetRequiredService<RunningPackageService>();
        if (
            runningPackageService.RunningPackages.Count > 0
            && e.CloseReason is WindowCloseReason.WindowClosing
        )
        {
            e.Cancel = true;

            var dialog = CreateExitConfirmDialog();
            Dispatcher
                .UIThread.InvokeAsync(async () =>
                {
                    if (
                        (TaskDialogStandardResult)await dialog.ShowAsync(true) == TaskDialogStandardResult.Yes
                    )
                    {
                        App.Services.GetRequiredService<MainWindow>().Hide();
                        App.Shutdown();
                    }
                })
                .SafeFireAndForget();
        }

        base.OnClosing(e);
    }

    private static TaskDialog CreateExitConfirmDialog()
    {
        var dialog = DialogHelper.CreateTaskDialog(
            Languages.Resources.Label_ConfirmExit,
            Languages.Resources.Label_ConfirmExitDetail
        );

        dialog.ShowProgressBar = false;
        dialog.FooterVisibility = TaskDialogFooterVisibility.Never;

        dialog.Buttons = new List<TaskDialogButton>
        {
            new("Exit", TaskDialogStandardResult.Yes),
            TaskDialogButton.CancelButton,
        };
        dialog.Buttons[0].IsDefault = true;

        return dialog;
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
        Dispatcher.UIThread.Post(() =>
            navigationService.NavigateTo(
                vm.Pages[0],
                new BetterSlideNavigationTransition { Effect = SlideNavigationTransitionEffect.FromBottom }
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

    private void SetDefaultFonts()
    {
        if (App.Current is not null)
        {
            FontFamily = App.Current.GetPlatformDefaultFontFamily();
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
            WindowTransparencyLevel.Blur,
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

    private void NavigationView_OnBackRequested(object? sender, NavigationViewBackRequestedEventArgs e)
    {
        navigationService.GoBack();
    }
}
