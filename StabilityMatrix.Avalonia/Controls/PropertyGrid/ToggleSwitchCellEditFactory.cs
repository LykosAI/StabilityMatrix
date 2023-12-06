using Avalonia.Controls;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.Controls.Factories;
using Avalonia.PropertyGrid.Localization;

namespace StabilityMatrix.Avalonia.Controls;

internal class ToggleSwitchCellEditFactory : AbstractCellEditFactory
{
    // make this extend factor only effect on TestExtendPropertyGrid
    public override bool Accept(object accessToken)
    {
        return accessToken is BetterPropertyGrid;
    }

    public override Control? HandleNewProperty(PropertyCellContext context)
    {
        var propertyDescriptor = context.Property;
        var target = context.Target;

        if (propertyDescriptor.PropertyType != typeof(bool))
        {
            return null;
        }

        var control = new ToggleSwitch();
        control.SetLocalizeBinding(ToggleSwitch.OnContentProperty, "On");
        control.SetLocalizeBinding(ToggleSwitch.OffContentProperty, "Off");

        control.IsCheckedChanged += (s, e) =>
        {
            SetAndRaise(context, control, control.IsChecked);
        };

        return control;
    }

    public override bool HandlePropertyChanged(PropertyCellContext context)
    {
        var propertyDescriptor = context.Property;
        var target = context.Target;
        var control = context.CellEdit;

        if (propertyDescriptor.PropertyType != typeof(bool))
        {
            return false;
        }

        ValidateProperty(control, propertyDescriptor, target);

        if (control is ToggleSwitch ts)
        {
            ts.IsChecked = (bool)(propertyDescriptor.GetValue(target) ?? false);

            return true;
        }

        return false;
    }
}
