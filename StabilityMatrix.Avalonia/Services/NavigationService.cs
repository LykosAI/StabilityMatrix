using System;
using System.Linq;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Services;

public class NavigationService : INavigationService
{
    private Frame? _frame;
    
    /// <inheritdoc />
    public void SetFrame(Frame frame)
    {
        _frame = frame;
    }

    /// <inheritdoc />
    public void NavigateTo<TViewModel>(NavigationTransitionInfo? transitionInfo = null) where TViewModel : ViewModelBase
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("SetFrame was not called before NavigateTo.");
        }
        
        _frame.NavigateToType(typeof(TViewModel),
            null,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            });

        if (!typeof(TViewModel).IsAssignableTo(typeof(PageViewModelBase)))
            return;
        
        if (App.Services.GetService(typeof(MainWindowViewModel)) is MainWindowViewModel mainViewModel)
        {
            mainViewModel.SelectedCategory =
                mainViewModel.Pages.FirstOrDefault(x => x.GetType() == typeof(TViewModel));
        }
    }

    /// <inheritdoc />
    public void NavigateTo(ViewModelBase viewModel, NavigationTransitionInfo? transitionInfo = null)
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("SetFrame was not called before NavigateTo.");
        }
        
        _frame.NavigateFromObject(viewModel,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            });
    }
}
