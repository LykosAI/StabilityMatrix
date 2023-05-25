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
    public sealed partial class InstallPage : Page
    {
        public InstallPage()
        { 
            InitializeComponent();
            DataContext = new InstallerViewModel();
        }

        private async void InstallPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            await ((InstallerViewModel) DataContext).OnLoaded(); 
        }
    }
}
