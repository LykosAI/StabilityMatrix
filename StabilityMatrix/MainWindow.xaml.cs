using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            RootNavigation.Navigating += (_, _) => Debug.WriteLine("Navigating");
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(LaunchPage));
        }
    }
}
