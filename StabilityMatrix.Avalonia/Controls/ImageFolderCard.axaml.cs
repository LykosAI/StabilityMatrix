using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls;

public class ImageFolderCard : DropTargetTemplatedControlBase
{
    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ViewModelBase vm)
        {
            vm.OnLoaded();
            Dispatcher.UIThread
                .InvokeAsync(async () =>
                {
                    await vm.OnLoadedAsync();
                })
                .SafeFireAndForget();
        }
    }

    /// <inheritdoc />
    protected override void DropHandler(object? sender, DragEventArgs e)
    {
        base.DropHandler(sender, e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void DragOverHandler(object? sender, DragEventArgs e)
    {
        base.DragOverHandler(sender, e);
        e.Handled = true;
    }
}
