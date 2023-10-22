using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
public partial class DownloadResourceDialog : UserControl
{
    public DownloadResourceDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LicenseButton_OnTapped(object? sender, TappedEventArgs e)
    {
        var url = ((DownloadResourceViewModel)DataContext!).Resource.LicenseUrl;
        ProcessRunner.OpenUrl(url!.ToString());
    }
}
