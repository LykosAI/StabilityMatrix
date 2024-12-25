using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<SettingsPage>]
public partial class SettingsPage : UserControlBase, IHandleNavigation
{
    private readonly INavigationService<SettingsViewModel> settingsNavigationService;

    private bool hasLoaded;

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

        if (!hasLoaded)
        {
            // Initial load, navigate to first page
            Dispatcher.UIThread.Post(
                () =>
                    settingsNavigationService.NavigateTo(
                        ViewModel.SubPages[0],
                        new SuppressNavigationTransitionInfo()
                    )
            );

            hasLoaded = true;
        }
    }

    private void NavigationService_OnTypedNavigation(object? sender, TypedNavigationEventArgs e)
    {
        ViewModel.CurrentPage = ViewModel.SubPages.FirstOrDefault(x => x.GetType() == e.ViewModelType);
    }

    private async void FrameView_Navigated(object? sender, NavigationEventArgs args)
    {
        if (args.Content is not PageViewModelBase vm)
        {
            return;
        }

        ViewModel.CurrentPage = vm;
    }

    private async void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        // Skip if already on same page
        if (args.Item is not PageViewModelBase viewModel || viewModel == ViewModel.CurrentPage)
        {
            return;
        }

        settingsNavigationService.NavigateTo(viewModel, BetterSlideNavigationTransition.PageSlideFromLeft);
    }

    public bool GoBack()
    {
        return settingsNavigationService.GoBack();
    }
}
