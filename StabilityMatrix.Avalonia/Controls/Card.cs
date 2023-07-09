using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace StabilityMatrix.Avalonia.Controls;

public class Card : Expander
{
    public Card()
    {
        // Expander /template/ ToggleButton#PART_toggle
        var customStyle = new Style(x =>
            x.OfType<Expander>().Template().OfType<ToggleButton>().Name("PART_toggle"));
        
        customStyle.Setters.Add(new Setter
        {
            Property = IsVisibleProperty,
            Value = false
        });
        
        Styles.Add(customStyle);
    }
}
