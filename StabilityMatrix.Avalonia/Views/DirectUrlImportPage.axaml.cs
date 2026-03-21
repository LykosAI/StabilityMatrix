using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

namespace StabilityMatrix.Avalonia.Views;

[RegisterTransient<DirectUrlImportPage>]
public partial class DirectUrlImportPage : UserControlBase
{
    public DirectUrlImportPage()
    {
        InitializeComponent();
    }

    public DirectUrlImportPage(DirectUrlImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
