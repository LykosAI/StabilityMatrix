using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
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
