using Avalonia.Interactivity;
using FluentAvalonia.UI.Windowing;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Controls;

public class AppWindowBase : AppWindow
{
    protected AppWindowBase()
    {
        AddHandler(LoadedEvent, OnLoaded);
    }
    
    public virtual async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModelBase viewModel) return;
        
        // ReSharper disable once MethodHasAsyncOverload
        viewModel.OnLoaded();
        await viewModel.OnLoadedAsync();
    }
}
