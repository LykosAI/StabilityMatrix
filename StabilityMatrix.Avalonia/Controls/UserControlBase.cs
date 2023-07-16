using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Controls;

public class UserControlBase : UserControl
{
    static UserControlBase()
    {
        LoadedEvent.AddClassHandler<UserControlBase>(
            (cls, args) => cls.OnLoadedEvent(args));
    }

    protected virtual void OnLoadedEvent(RoutedEventArgs? e)
    {
        if (DataContext is not ViewModelBase viewModel) return;
        
        viewModel.OnLoaded();
        // Can't block here so we'll run as async on UI thread
        Dispatcher.UIThread.InvokeAsync(async () => await viewModel.OnLoadedAsync());
    }
}
