using Avalonia.Markup.Xaml;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<RefreshBadge>]
public partial class RefreshBadge : UserControlBase
{
    public RefreshBadge()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
