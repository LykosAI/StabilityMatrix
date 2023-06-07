using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Octokit;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Navigation;
using Wpf.Ui.Controls.Window;
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
            MainWindowViewModel mainWindowViewModel, ISettingsManager settingsManager, ISnackbarService snackbarService, INotificationBarService notificationBarService)
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
        }

        private void RootNavigation_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            settingsManager.SetNavExpanded(true);
        }

        private void RootNavigation_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            settingsManager.SetNavExpanded(false);
        }
    }
}
