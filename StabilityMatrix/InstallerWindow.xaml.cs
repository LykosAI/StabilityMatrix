using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.Window;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace StabilityMatrix
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InstallerWindow : FluentWindow
    {
        private readonly InstallerViewModel viewModel;

        public InstallerWindow(InstallerViewModel viewModel)
        {
            this.viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PackageInstalled += (_, _) => Close();
        }

        private async void InstallPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            await viewModel.OnLoaded();
        }
    }
}
