using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        
        if (Design.IsDesignMode) return;
        
        viewModel.OnLoaded();
        viewModel.OnLoadedAsync().Wait();
    }
}
