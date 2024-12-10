using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<AnalyticsOptInDialog>]
public partial class AnalyticsOptInDialog : UserControl
{
    public AnalyticsOptInDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
