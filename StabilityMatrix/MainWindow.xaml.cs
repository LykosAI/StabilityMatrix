using System.Diagnostics;
using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly MainWindowViewModel mainWindowViewModel;

        public MainWindow(IPageService pageService, IContentDialogService contentDialogService,
            MainWindowViewModel mainWindowViewModel)
        {
            InitializeComponent();

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
