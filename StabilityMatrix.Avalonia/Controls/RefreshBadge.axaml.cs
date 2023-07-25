using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Controls;

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
