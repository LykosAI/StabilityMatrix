using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Services;

public interface INavigationService
{
    /// <summary>
    /// Set the frame to use for navigation.
    /// </summary>
    void SetFrame(Frame frame);

    /// <summary>
    /// Navigate to the view of the given view model type.
    /// </summary>
    void NavigateTo<TViewModel>(NavigationTransitionInfo? transitionInfo = null) where TViewModel : ViewModelBase;
    
    /// <summary>
    /// Navigate to the view of the given view model.
    /// </summary>
    void NavigateTo(ViewModelBase viewModel, NavigationTransitionInfo? transitionInfo = null);
}
