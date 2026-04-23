using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterTransient<DirectUrlImportPage>]
public partial class DirectUrlImportPage : UserControlBase
{
    public DirectUrlImportPage()
    {
        InitializeComponent();
    }
}
