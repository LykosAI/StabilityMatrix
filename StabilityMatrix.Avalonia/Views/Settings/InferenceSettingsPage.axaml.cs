using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Views.Settings;

public partial class InferenceSettingsPage : UserControl
{
    public InferenceSettingsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}