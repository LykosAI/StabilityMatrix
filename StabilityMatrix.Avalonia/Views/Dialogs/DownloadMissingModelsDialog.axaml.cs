using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<DownloadMissingModelsDialog>]
public partial class DownloadMissingModelsDialog : UserControlBase
{
    public DownloadMissingModelsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
