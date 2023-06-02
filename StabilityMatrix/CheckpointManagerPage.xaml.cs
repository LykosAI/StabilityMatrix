using System.Windows.Controls;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix;

public partial class CheckpointManagerPage : Page
{
    public CheckpointManagerPage(CheckpointManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
