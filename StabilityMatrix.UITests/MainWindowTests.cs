using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;

namespace StabilityMatrix.UITests;

[UsesVerify]
public class MainWindowTests
{
    private static IServiceProvider Services => App.Services;

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
            return settings;
        }
    }

    private static (AppWindow, MainWindowViewModel) GetMainWindow()
    {
        var window = Services.GetRequiredService<MainWindow>();
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;

        window.SetDefaultFonts();

        App.VisualRoot = window;
        App.StorageProvider = window.StorageProvider;
        App.Clipboard = window.Clipboard ?? throw new NullReferenceException("Clipboard is null");

        return (window, viewModel);
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

    [AvaloniaFact]
    public Task MainWindowViewModel_ShouldOk()
    {
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();

        return Verify(viewModel, Settings);
    }

    [AvaloniaFact]
    public async Task MainWindow_ShouldOpen()
    {
        var (window, vm) = GetMainWindow();

        window.Show();

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
        continueButton.Command?.Execute(null);

        // Find the one click install dialog
        var oneClickDialog = await WaitHelper.WaitForConditionAsync(
            () => GetWindowDialog(window),
            d => d?.Content is OneClickInstallDialog
        );
        Assert.NotNull(oneClickDialog);

        await Verify(window, Settings);
    }
}
