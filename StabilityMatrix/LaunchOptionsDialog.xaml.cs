using System.Windows.Controls;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class LaunchOptionsDialog : ContentDialog
{
    public LaunchOptionsDialog(ContentPresenter contentPresenter) : base(contentPresenter)
    {
        InitializeComponent();
    }
}
