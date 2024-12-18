using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<InferenceSettingsPage>]
public partial class InferenceSettingsPage : UserControlBase
{
    public InferenceSettingsPage()
    {
        InitializeComponent();
    }
}
