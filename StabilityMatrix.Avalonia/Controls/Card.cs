using System;
using Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Controls;

public class Card : Expander
{
    protected override Type StyleKeyOverride => typeof(Expander);

    public Card()
    {
        IsExpanded = true;
    }
}
