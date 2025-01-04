using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterTransient<AnalyticsSettingsPage>]
public partial class AnalyticsSettingsPage : UserControl
{
    public AnalyticsSettingsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
