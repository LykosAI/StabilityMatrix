using System;
using Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Controls;

public class Card : ContentControl
{
    protected override Type StyleKeyOverride => typeof(Card);

    public Card()
    {
        MinHeight = 8;
        MinWidth = 8;
    }
}
