using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class SelectModelVersionDialog : ContentDialog
{
    private SelectModelVersionDialogViewModel viewModel;
    
    public SelectModelVersionDialog(IContentDialogService dialogService,
        SelectModelVersionDialogViewModel viewModel) : base(dialogService.GetContentPresenter())
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
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

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Hide(ContentDialogResult.Secondary);
    }

    private void Import_OnClick(object sender, RoutedEventArgs e)
    {
        Hide(ContentDialogResult.Primary);
    }
}
