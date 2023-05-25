using System.Windows.Controls;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix;

public sealed partial class LaunchPage : Page
{
    public LaunchPage(LaunchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SelectPackageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        
    }
}
