using System.Diagnostics;
using System.Windows;
using StabilityMatrix.Helper;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Navigation;
using Wpf.Ui.Controls.Window;

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
            MainWindowViewModel mainWindowViewModel, ISettingsManager settingsManager)
        {
            InitializeComponent();

            this.mainWindowViewModel = mainWindowViewModel;
            this.settingsManager = settingsManager;

            DataContext = mainWindowViewModel;

            RootNavigation.Navigating += (_, _) => Debug.WriteLine("Navigating");
            RootNavigation.SetPageService(pageService);

            contentDialogService.SetContentPresenter(RootContentDialog);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(LaunchPage));
            mainWindowViewModel.OnLoaded();
            RootNavigation.IsPaneOpen = settingsManager.Settings.IsNavExpanded;
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
