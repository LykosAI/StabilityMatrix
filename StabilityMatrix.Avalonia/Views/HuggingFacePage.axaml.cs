using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<HuggingFacePage>]
public partial class HuggingFacePage : UserControlBase
{
    public HuggingFacePage()
    {
        InitializeComponent();
    }
}
