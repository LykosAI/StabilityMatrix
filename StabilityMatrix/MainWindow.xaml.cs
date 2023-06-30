using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Navigation;
using Wpf.Ui.Controls.Window;
using Application = System.Windows.Application;
using EventManager = StabilityMatrix.Helper.EventManager;
using ISnackbarService = Wpf.Ui.Contracts.ISnackbarService;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly MainWindowViewModel mainWindowViewModel;
        private readonly ISettingsManager settingsManager;

        public MainWindow(IPageService pageService, IContentDialogService contentDialogService,
            MainWindowViewModel mainWindowViewModel, ISettingsManager settingsManager,
            ISnackbarService snackbarService, INotificationBarService notificationBarService)
        {
            InitializeComponent();

            this.mainWindowViewModel = mainWindowViewModel;
            this.settingsManager = settingsManager;

            DataContext = mainWindowViewModel;

            RootNavigation.Navigating += (_, _) => Debug.WriteLine("Navigating");
            RootNavigation.SetPageService(pageService);

            snackbarService.SetSnackbarControl(RootSnackbar);
            notificationBarService.SetSnackbarControl(NotificationSnackbar);
            contentDialogService.SetContentPresenter(RootContentDialog);

            EventManager.Instance.PageChangeRequested += InstanceOnPageChangeRequested;
        }

        private void InstanceOnPageChangeRequested(object? sender, Type e)
        {
            RootNavigation.Navigate(e);
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(LaunchPage));
            RootNavigation.IsPaneOpen = settingsManager.Settings.IsNavExpanded;
            await mainWindowViewModel.OnLoaded();
            ResizeWindow();
            ObserveSizeChanged();
        }

        private void RootNavigation_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            if (settingsManager.TryFindLibrary())
            {
                settingsManager.SetNavExpanded(true);
            }
        }

        private void RootNavigation_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            if (settingsManager.TryFindLibrary())
            {
                settingsManager.SetNavExpanded(false);
            }
        }

        private void MainWindow_OnClosed(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ObserveSizeChanged()
        {
            var observableSizeChanges = Observable
                .FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                    h => SizeChanged += h,
                    h => SizeChanged -= h)
                .Select(x => x.EventArgs)
                .Throttle(TimeSpan.FromMilliseconds(150));

            observableSizeChanges
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(args =>
                {
                    if (args is {HeightChanged: false, WidthChanged: false}) return;
                    
                    var interopHelper = new WindowInteropHelper(this);
                    var placement = ScreenExtensions.GetPlacement(interopHelper.Handle);
                    settingsManager.SetPlacement(placement.ToString());
                });
        }

        private void ResizeWindow()
        {
            var interopHelper = new WindowInteropHelper(this);

            if (string.IsNullOrWhiteSpace(settingsManager.Settings.Placement))
                return;
            var placement = new ScreenExtensions.WINDOWPLACEMENT();
            placement.ReadFromBase64String(settingsManager.Settings.Placement);

            var primaryMonitorScaling = ScreenExtensions.GetScalingForPoint(new System.Drawing.Point(1, 1));
            var currentMonitorScaling = ScreenExtensions.GetScalingForPoint(new System.Drawing.Point(placement.rcNormalPosition.left, placement.rcNormalPosition.top));
            var rescaleFactor = currentMonitorScaling / primaryMonitorScaling;
            double width = placement.rcNormalPosition.right - placement.rcNormalPosition.left;
            double height = placement.rcNormalPosition.bottom - placement.rcNormalPosition.top;
            placement.rcNormalPosition.right = placement.rcNormalPosition.left + (int)(width / rescaleFactor + 0.5);
            placement.rcNormalPosition.bottom = placement.rcNormalPosition.top + (int)(height / rescaleFactor + 0.5);
            ScreenExtensions.SetPlacement(interopHelper.Handle, placement);
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            var interopHelper = new WindowInteropHelper(this);
            var placement = ScreenExtensions.GetPlacement(interopHelper.Handle);
            settingsManager.SetPlacement(placement.ToString());
        }
    }
}
