using System.Windows.Controls;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix;

public sealed partial class TextToImagePage : Page
{
    public TextToImagePage(TextToImageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
