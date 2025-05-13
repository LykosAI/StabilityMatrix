using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<AnalyticsOptInDialog>]
public partial class AnalyticsOptInDialog : UserControlBase
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
