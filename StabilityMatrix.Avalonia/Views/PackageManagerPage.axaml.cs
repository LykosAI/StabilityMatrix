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
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<PackageManagerPage>]
public partial class PackageManagerPage : UserControlBase, IHandleNavigation
{
    private readonly INavigationService<PackageManagerViewModel> packageNavigationService;

    private bool hasLoaded;

    private PackageManagerViewModel ViewModel => (PackageManagerViewModel)DataContext!;

    [DesignOnly(true)]
    [Obsolete("For XAML use only", true)]
    public PackageManagerPage()
        : this(App.Services.GetRequiredService<INavigationService<PackageManagerViewModel>>()) { }

    public PackageManagerPage(INavigationService<PackageManagerViewModel> packageNavigationService)
    {
        this.packageNavigationService = packageNavigationService;

        InitializeComponent();

        AddHandler(Frame.NavigatedToEvent, OnNavigatedTo, RoutingStrategies.Direct);

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

    /// <summary>
    /// Handle navigation events to this page
    /// </summary>
    private void OnNavigatedTo(object? sender, NavigationEventArgs args)
    {
        if (args.Parameter is PackageManagerNavigationOptions { OpenInstallerDialog: true } options)
        {
            var vm = (PackageManagerViewModel)DataContext!;

            Dispatcher.UIThread.Post(
                () =>
                {
                    // Navigate to the installer page
                    packageNavigationService.NavigateTo<PackageInstallBrowserViewModel>();

                    // Select the package
                    vm.SubPages.OfType<PackageInstallBrowserViewModel>()
                        .First()
                        .OnPackageSelected(options.InstallerSelectedPackage);
                },
                DispatcherPriority.Send
            );
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
