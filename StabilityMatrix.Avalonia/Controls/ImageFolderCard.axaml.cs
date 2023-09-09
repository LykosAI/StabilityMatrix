using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Controls;

public class ImageFolderCard : TemplatedControl
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
}
