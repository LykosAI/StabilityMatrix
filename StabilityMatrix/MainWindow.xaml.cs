using System.Diagnostics;
using System.Windows;
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
        private readonly IPageService pageService;
        private readonly ISettingsManager settingsManager;
        private readonly IContentDialogService contentDialogService;
        private readonly MainWindowViewModel mainWindowViewModel;

        public MainWindow(IPageService pageService, ISettingsManager settingsManager, 
            IContentDialogService contentDialogService, MainWindowViewModel mainWindowViewModel)
        {
            InitializeComponent();

            this.pageService = pageService;
            this.settingsManager = settingsManager;
            this.contentDialogService = contentDialogService;
            this.mainWindowViewModel = mainWindowViewModel;

            DataContext = mainWindowViewModel;
            
            RootNavigation.Navigating += (_, _) => Debug.WriteLine("Navigating");
            RootNavigation.SetPageService(pageService);
            
            contentDialogService.SetContentPresenter(RootContentDialog);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(LaunchPage));
            mainWindowViewModel.OnLoaded();
        }
        
    }
}
