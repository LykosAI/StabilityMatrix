using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix;

public partial class CheckpointManagerPage : Page
{
    private readonly CheckpointManagerViewModel viewModel;
    public CheckpointManagerPage(CheckpointManagerViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void CheckpointManagerPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.OnLoaded();
    }

    /// <summary>
    /// Bubbles the mouse wheel event up to the parent.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void VirtualizingGridView_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        if (((Control)sender).Parent is UIElement parent)
        {
            parent.RaiseEvent(eventArg);
        }
    }
}
