using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;

namespace StabilityMatrix.Avalonia.Services;

public class NavigationService
{
    public static NavigationService Instance { get; } = new();

    public Control PreviousPage { get; set; }

    private NavigationService()
    {
        // dont call this
    }

    public void SetFrame(Frame f)
    {
        _frame = f;
    }

    public void SetOverlayHost(Panel p)
    {
        _overlayHost = p;
    }

    public void Navigate(Type t)
    {
        _frame.Navigate(t);
    }

    public void NavigateFromContext(object dataContext, NavigationTransitionInfo transitionInfo = null)
    {
        _frame.NavigateFromObject(dataContext,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            });
    }

    // public void ShowControlDefinitionOverlay(Type targetType)
    // {
    //     if (_overlayHost != null)
    //     {
    //         (_overlayHost.Children[0] as ControlDefinitionOverlay).TargetType = targetType;
    //         (_overlayHost.Children[0] as ControlDefinitionOverlay).Show();
    //     }
    // }

    public void ClearOverlay()
    {
        _overlayHost?.Children.Clear();

    }

    private Frame _frame;
    private Panel _overlayHost;
}
