using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[RegisterTransient<PackageInstallDetailView>]
public partial class PackageInstallDetailView : UserControlBase
{
    public PackageInstallDetailView()
    {
        InitializeComponent();
    }
}
