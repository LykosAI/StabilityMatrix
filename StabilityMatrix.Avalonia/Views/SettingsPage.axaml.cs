using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class SettingsPage : UserControlBase
{
    private readonly INavigationService<SettingsViewModel> settingsNavigationService;

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext!;

    [DesignOnly(true)]
    [Obsolete("For XAML use only", true)]
    public SettingsPage()
        : this(App.Services.GetRequiredService<INavigationService<SettingsViewModel>>()) { }

    public SettingsPage(INavigationService<SettingsViewModel> settingsNavigationService)
    {
        this.settingsNavigationService = settingsNavigationService;

        InitializeComponent();

        settingsNavigationService.SetFrame(FrameView);
        settingsNavigationService.TypedNavigation += NavigationService_OnTypedNavigation;
        FrameView.Navigated += FrameView_Navigated;
        BreadcrumbBar.ItemClicked += BreadcrumbBar_ItemClicked;
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        Dispatcher.UIThread.Post(
            () =>
                settingsNavigationService.NavigateTo(
                    ViewModel.SubPages[0],
                    new BetterSlideNavigationTransition
                    {
                        Effect = SlideNavigationTransitionEffect.FromBottom
                    }
                )
        );
    }

    private void NavigationService_OnTypedNavigation(object? sender, TypedNavigationEventArgs e)
    {
        ViewModel.CurrentPage = ViewModel.SubPages.FirstOrDefault(
            x => x.GetType() == e.ViewModelType
        );
    }

    private async void FrameView_Navigated(object? sender, NavigationEventArgs args)
    {
        if (args.Content is not PageViewModelBase vm)
        {
            return;
        }

        ViewModel.CurrentPage = vm;
    }

    private async void BreadcrumbBar_ItemClicked(
        BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args
    )
    {
        if (args.Item is not PageViewModelBase viewModel)
        {
            return;
        }

        settingsNavigationService.NavigateTo(
            viewModel,
            new BetterSlideNavigationTransition
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            }
        );
    }
}
