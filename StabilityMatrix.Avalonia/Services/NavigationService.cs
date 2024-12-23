using System;
using System.Linq;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<INavigationService<MainWindowViewModel>, NavigationService<MainWindowViewModel>>]
[RegisterSingleton<INavigationService<SettingsViewModel>, NavigationService<SettingsViewModel>>]
[RegisterSingleton<INavigationService<PackageManagerViewModel>, NavigationService<PackageManagerViewModel>>]
public class NavigationService<T> : INavigationService<T>
{
    private Frame? _frame;

    public event EventHandler<TypedNavigationEventArgs>? TypedNavigation;

    /// <inheritdoc />
    public void SetFrame(Frame frame)
    {
        _frame = frame;
    }

    /// <inheritdoc />
    public void NavigateTo<TViewModel>(NavigationTransitionInfo? transitionInfo = null, object? param = null)
        where TViewModel : ViewModelBase
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("SetFrame was not called before NavigateTo.");
        }

        if (App.Services.GetService(typeof(ISettingsManager)) is ISettingsManager settingsManager)
        {
            // Handle animation scale
            switch (transitionInfo)
            {
                // If the transition info is null or animation scale is 0, suppress the transition
                case null:
                case BaseTransitionInfo when settingsManager.Settings.AnimationScale == 0f:
                    transitionInfo = new SuppressNavigationTransitionInfo();
                    break;
                case BaseTransitionInfo baseTransitionInfo:
                    baseTransitionInfo.Duration *= settingsManager.Settings.AnimationScale;
                    break;
            }
        }

        _frame.NavigateToType(
            typeof(TViewModel),
            param,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            }
        );

        TypedNavigation?.Invoke(this, new TypedNavigationEventArgs { ViewModelType = typeof(TViewModel) });
    }

    /// <inheritdoc />
    public void NavigateTo(
        Type viewModelType,
        NavigationTransitionInfo? transitionInfo = null,
        object? param = null
    )
    {
        if (!viewModelType.IsAssignableTo(typeof(ViewModelBase)))
        {
            // ReSharper disable once LocalizableElement
            throw new ArgumentException("Type must be a ViewModelBase.", nameof(viewModelType));
        }

        if (_frame is null)
        {
            throw new InvalidOperationException("SetFrame was not called before NavigateTo.");
        }

        if (App.Services.GetService(typeof(ISettingsManager)) is ISettingsManager settingsManager)
        {
            // Handle animation scale
            switch (transitionInfo)
            {
                // If the transition info is null or animation scale is 0, suppress the transition
                case null:
                case BaseTransitionInfo when settingsManager.Settings.AnimationScale == 0f:
                    transitionInfo = new SuppressNavigationTransitionInfo();
                    break;
                case BaseTransitionInfo baseTransitionInfo:
                    baseTransitionInfo.Duration *= settingsManager.Settings.AnimationScale;
                    break;
            }
        }

        _frame.NavigateToType(
            viewModelType,
            param,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            }
        );

        TypedNavigation?.Invoke(this, new TypedNavigationEventArgs { ViewModelType = viewModelType });
    }

    /// <inheritdoc />
    public void NavigateTo(ViewModelBase viewModel, NavigationTransitionInfo? transitionInfo = null)
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("SetFrame was not called before NavigateTo.");
        }

        if (App.Services.GetService(typeof(ISettingsManager)) is ISettingsManager settingsManager)
        {
            // Handle animation scale
            switch (transitionInfo)
            {
                // If the transition info is null or animation scale is 0, suppress the transition
                case null:
                case BaseTransitionInfo when settingsManager.Settings.AnimationScale == 0f:
                    transitionInfo = new SuppressNavigationTransitionInfo();
                    break;
                case BaseTransitionInfo baseTransitionInfo:
                    baseTransitionInfo.Duration *= settingsManager.Settings.AnimationScale;
                    break;
            }
        }

        _frame.NavigateFromObject(
            viewModel,
            new FrameNavigationOptions
            {
                IsNavigationStackEnabled = true,
                TransitionInfoOverride = transitionInfo ?? new SuppressNavigationTransitionInfo()
            }
        );

        TypedNavigation?.Invoke(
            this,
            new TypedNavigationEventArgs { ViewModelType = viewModel.GetType(), ViewModel = viewModel }
        );
    }

    public bool GoBack()
    {
        if (_frame?.Content is IHandleNavigation navigationHandler)
        {
            var wentBack = navigationHandler.GoBack();
            if (wentBack)
            {
                return true;
            }
        }

        if (_frame is not { CanGoBack: true })
            return false;

        TypedNavigation?.Invoke(
            this,
            new TypedNavigationEventArgs
            {
                ViewModelType = _frame.BackStack.Last().SourcePageType,
                ViewModel = _frame.BackStack.Last().Context
            }
        );

        _frame.GoBack();
        return true;
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public object? CurrentPageDataContext => (_frame?.Content as Control)?.DataContext;
}
