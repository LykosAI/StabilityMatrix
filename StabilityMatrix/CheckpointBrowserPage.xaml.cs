using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls;

namespace StabilityMatrix;

public partial class CheckpointBrowserPage : Page
{
    public CheckpointBrowserPage(CheckpointBrowserViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
