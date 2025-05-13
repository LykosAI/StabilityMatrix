using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<DownloadResourceDialog>]
public partial class DownloadResourceDialog : UserControlBase
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
