using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Navigation;
using Wpf.Ui.Controls.Window;
using Application = System.Windows.Application;
using EventManager = StabilityMatrix.Core.Helper.EventManager;
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
            ObserveSizeChanged();
        }

        private void RootNavigation_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            if (settingsManager.IsLibraryDirSet)
            {
                settingsManager.Transaction(s => s.IsNavExpanded = true);
            }
        }

        private void RootNavigation_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            if (settingsManager.IsLibraryDirSet)
            {
                settingsManager.Transaction(s => s.IsNavExpanded = false);
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
                    settingsManager.Transaction(s => s.Placement = placement.ToString());
                });
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            var interopHelper = new WindowInteropHelper(this);
            var placement = ScreenExtensions.GetPlacement(interopHelper.Handle);

            if (settingsManager.IsLibraryDirSet)
            {
                settingsManager.Transaction(s => s.Placement = placement.ToString());
            }
        }
    }
}
