using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.UITests.Extensions;

namespace StabilityMatrix.UITests;

[UsesVerify]
[Collection("TempDir")]
[TestCaseOrderer("StabilityMatrix.UITests.PriorityOrderer", "StabilityMatrix.UITests")]
public class MainWindowTests
{
    private static IServiceProvider Services => App.Services;

    private static (AppWindow, MainWindowViewModel)? currentMainWindow;

    private static VerifySettings Settings
    {
        get
        {
            var settings = new VerifySettings();
            settings.IgnoreMembers<MainWindowViewModel>(
                vm => vm.Pages,
                vm => vm.FooterPages,
                vm => vm.CurrentPage
            );
            settings.DisableDiff();
            return settings;
        }
    }

    private static (AppWindow, MainWindowViewModel) GetMainWindow()
    {
        if (currentMainWindow is not null)
        {
            return currentMainWindow.Value;
        }

        var window = Services.GetRequiredService<MainWindow>();
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;

        window.SetDefaultFonts();
        window.Width = 1400;
        window.Height = 900;

        App.VisualRoot = window;
        App.StorageProvider = window.StorageProvider;
        App.Clipboard = window.Clipboard ?? throw new NullReferenceException("Clipboard is null");

        currentMainWindow = (window, viewModel);
        return currentMainWindow.Value;
    }

    private static BetterContentDialog? GetWindowDialog(Visual window)
    {
        return window
            .FindDescendantOfType<VisualLayerManager>()
            ?.FindDescendantOfType<OverlayLayer>()
            ?.FindDescendantOfType<DialogHost>()
            ?.FindDescendantOfType<LayoutTransformControl>()
            ?.FindDescendantOfType<VisualLayerManager>()
            ?.FindDescendantOfType<BetterContentDialog>();
    }

    private static IEnumerable<BetterContentDialog> EnumerateWindowDialogs(Visual window)
    {
        return window
                .FindDescendantOfType<VisualLayerManager>()
                ?.FindDescendantOfType<OverlayLayer>()
                ?.FindDescendantOfType<DialogHost>()
                ?.FindDescendantOfType<LayoutTransformControl>()
                ?.FindDescendantOfType<VisualLayerManager>()
                ?.GetVisualDescendants()
                .OfType<BetterContentDialog>() ?? Enumerable.Empty<BetterContentDialog>();
    }

    private async Task<(BetterContentDialog, T)> WaitForDialog<T>(Visual window)
        where T : Control
    {
        var dialogs = await WaitHelper.WaitForConditionAsync(
            () => EnumerateWindowDialogs(window).ToList(),
            list => list.Any(dialog => dialog.Content is T)
        );

        if (dialogs.Count == 0)
        {
            throw new InvalidOperationException("No dialogs found");
        }

        var contentDialog = dialogs.First(dialog => dialog.Content is T);

        return (contentDialog, contentDialog.Content as T)!;
    }

    [AvaloniaFact, TestPriority(1)]
    public async Task MainWindow_ShouldOpen()
    {
        var (window, _) = GetMainWindow();

        window.Show();

        await Task.Delay(300);

        Dispatcher.UIThread.RunJobs();

        // Find the select data directory dialog
        var selectDataDirectoryDialog = await WaitHelper.WaitForNotNullAsync(
            () => GetWindowDialog(window)
        );
        Assert.NotNull(selectDataDirectoryDialog);

        // Click continue button
        var continueButton = selectDataDirectoryDialog
            .GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content as string == "Continue");

        await window.ClickTargetAsync(continueButton);

        // Find the one click install dialog
        var oneClickDialog = await WaitHelper.WaitForConditionAsync(
            () => GetWindowDialog(window),
            d => d?.Content is OneClickInstallDialog
        );
        Assert.NotNull(oneClickDialog);

        await Task.Delay(1800);

        await Verify(window, Settings);
    }

    [AvaloniaFact, TestPriority(2)]
    public async Task MainWindowViewModel_ShouldOk()
    {
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();

        await Verify(viewModel, Settings);
    }
}
