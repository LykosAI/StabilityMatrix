using System.Windows;

namespace StabilityMatrix.Interactions;

public class EventTriggerWithoutPropagation : Microsoft.Xaml.Behaviors.EventTrigger
{
    protected override void OnEvent(System.EventArgs eventArgs)
    {
        // Prevent event from propagating to parent
        if (eventArgs is RoutedEventArgs routedEventArgs)
        {
            routedEventArgs.Handled = true;
        }
        base.OnEvent(eventArgs);
    }
}
