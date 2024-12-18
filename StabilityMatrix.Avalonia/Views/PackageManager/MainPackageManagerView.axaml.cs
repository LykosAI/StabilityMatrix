using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using MainPackageManagerViewModel = StabilityMatrix.Avalonia.ViewModels.PackageManager.MainPackageManagerViewModel;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[RegisterSingleton<MainPackageManagerView>]
public partial class MainPackageManagerView : UserControlBase
{
    public MainPackageManagerView()
    {
        InitializeComponent();

        AddHandler(Frame.NavigatedToEvent, OnNavigatedTo, RoutingStrategies.Direct);
        EventManager.Instance.OneClickInstallFinished += OnOneClickInstallFinished;
    }

    private void OnOneClickInstallFinished(object? sender, bool skipped)
    {
        if (skipped)
            return;

        Dispatcher.UIThread.Invoke(() =>
        {
            var target = this.FindDescendantOfType<UniformGrid>()
                ?.GetVisualChildren()
                .OfType<Button>()
                .FirstOrDefault(x => x is { Name: "LaunchButton" });

            if (target == null)
                return;

            var teachingTip = this.FindControl<TeachingTip>("LaunchTeachingTip");
            if (teachingTip == null)
                return;

            teachingTip.Target = target;
            teachingTip.IsOpen = true;
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Handle navigation events to this page
    /// </summary>
    private void OnNavigatedTo(object? sender, NavigationEventArgs args)
    {
        if (args.Parameter is PackageManagerNavigationOptions { OpenInstallerDialog: true } options)
        {
            var vm = (MainPackageManagerViewModel)DataContext!;
            Dispatcher.UIThread.Invoke(() =>
            {
                vm.ShowInstallDialog(options.InstallerSelectedPackage);
            });
        }
    }
}
