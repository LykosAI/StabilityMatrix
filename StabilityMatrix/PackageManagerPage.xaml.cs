using System.Windows;
using System.Windows.Controls;
using StabilityMatrix.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace StabilityMatrix
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PackageManagerPage : Page
    {
        private readonly PackageManagerViewModel viewModel;

        public PackageManagerPage(PackageManagerViewModel viewModel)
        {
            this.viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
        }

        private async void InstallPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            await viewModel.OnLoaded();
        }
    }
}
