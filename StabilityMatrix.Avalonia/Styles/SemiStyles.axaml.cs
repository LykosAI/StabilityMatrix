using System;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.Styles;

public partial class SemiStyles : global::Avalonia.Styling.Styles
{
    public SemiStyles(IServiceProvider? provider = null)
    {
        AvaloniaXamlLoader.Load(provider, this);
    }
}
