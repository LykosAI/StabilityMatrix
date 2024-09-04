using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Settings;

[Transient]
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
