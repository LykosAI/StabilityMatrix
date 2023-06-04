using System.Windows;
using StabilityMatrix.Services;
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
        private readonly InstallerWindowDialogService dialogService;

        public InstallerWindow(InstallerViewModel viewModel, InstallerWindowDialogService dialogService)
        {
            this.viewModel = viewModel;
            this.dialogService = dialogService;
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PackageInstalled += (_, _) => Close();
            
            dialogService.SetContentPresenter(InstallerContentDialog);
        }

        private async void InstallPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            await viewModel.OnLoaded();
        }
    }
}
