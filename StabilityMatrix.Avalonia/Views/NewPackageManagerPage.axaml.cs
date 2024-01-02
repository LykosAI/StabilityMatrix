using System;
using System.ComponentModel;
using System.Linq;
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
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class NewPackageManagerPage : UserControlBase, IHandleNavigation
{
    private readonly INavigationService<NewPackageManagerViewModel> packageNavigationService;

    private bool hasLoaded;

    private NewPackageManagerViewModel ViewModel => (NewPackageManagerViewModel)DataContext!;

    [DesignOnly(true)]
    [Obsolete("For XAML use only", true)]
    public NewPackageManagerPage()
        : this(App.Services.GetRequiredService<INavigationService<NewPackageManagerViewModel>>()) { }

    public NewPackageManagerPage(INavigationService<NewPackageManagerViewModel> packageNavigationService)
    {
        this.packageNavigationService = packageNavigationService;

        InitializeComponent();

        packageNavigationService.SetFrame(FrameView);
        packageNavigationService.TypedNavigation += NavigationService_OnTypedNavigation;
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
                    packageNavigationService.NavigateTo(
                        ViewModel.SubPages[0],
                        new SuppressNavigationTransitionInfo()
                    )
            );

            hasLoaded = true;
        }
    }

    private void NavigationService_OnTypedNavigation(object? sender, TypedNavigationEventArgs e)
    {
        ViewModel.CurrentPage =
            ViewModel.SubPages.FirstOrDefault(x => x.GetType() == e.ViewModelType)
            ?? e.ViewModel as PageViewModelBase;
    }

    private void FrameView_Navigated(object? sender, NavigationEventArgs args)
    {
        if (args.Content is not PageViewModelBase vm)
        {
            return;
        }

        ViewModel.CurrentPage = vm;
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        // Skip if already on same page
        if (args.Item is not PageViewModelBase viewModel || viewModel == ViewModel.CurrentPage)
        {
            return;
        }

        packageNavigationService.NavigateTo(viewModel, BetterSlideNavigationTransition.PageSlideFromLeft);
    }

    public bool GoBack()
    {
        return packageNavigationService.GoBack();
    }
}
