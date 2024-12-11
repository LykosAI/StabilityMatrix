using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.UITests.Extensions;

namespace StabilityMatrix.UITests;

public class TestBase
{
    internal static IServiceProvider Services => App.Services;

    internal static (AppWindow, MainWindowViewModel)? currentMainWindow;

    internal virtual VerifySettings Settings
    {
        get
        {
            var settings = new VerifySettings();
            settings.IgnoreMembers<MainWindowViewModel>(
                vm => vm.Pages,
                vm => vm.FooterPages,
                vm => vm.CurrentPage
            );
            settings.IgnoreMember<UpdateViewModel>(vm => vm.CurrentVersionText);
            settings.DisableDiff();
            return settings;
        }
    }

    internal static (AppWindow, MainWindowViewModel) GetMainWindow()
    {
        if (currentMainWindow is not null)
        {
            return currentMainWindow.Value;
        }

        var window = Services.GetRequiredService<MainWindow>();
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        window.DataContext = viewModel;

        window.Width = 1400;
        window.Height = 900;

        App.VisualRoot = window;

        currentMainWindow = (window, viewModel);
        return currentMainWindow.Value;
    }

    internal static async Task DoInitialSetup()
    {
        var (window, _) = GetMainWindow();

        if (!window.IsVisible)
        {
            window.Show();
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
        }

        try
        {
            var dialog = await WaitHelper.WaitForNotNullAsync(() => GetWindowDialog(window));

            if (dialog.Content is SelectDataDirectoryDialog selectDataDirectoryDialog)
            {
                // Click continue button
                var continueButton = selectDataDirectoryDialog
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .First(b => b.Content as string == "Continue");

                await window.ClickTargetAsync(continueButton);
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();

                // Find the one click install dialog
                var oneClickDialog = await WaitHelper.WaitForConditionAsync(
                    () => GetWindowDialog(window),
                    d => d?.Content is OneClickInstallDialog
                );
                Assert.NotNull(oneClickDialog);

                var skipButton = oneClickDialog
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .First(b => b.Content as string == Resources.Label_SkipSetup);

                await window.ClickTargetAsync(skipButton);
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();
            }
            else if (dialog.Content is OneClickInstallDialog)
            {
                var skipButton = dialog
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .First(b => b.Content as string == Resources.Label_SkipSetup);

                await window.ClickTargetAsync(skipButton);
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();
            }
        }
        catch (TimeoutException)
        {
            // ignored
        }
    }

    internal static async Task CloseUpdateDialog()
    {
        var (window, _) = GetMainWindow();

        var updateTip = window.FindControl<TeachingTip>("UpdateAvailableTeachingTip")!;
        await window.ClickTargetAsync(updateTip);

        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
    }

    internal static BetterContentDialog? GetWindowDialog(Visual window)
    {
        return window
            .FindDescendantOfType<VisualLayerManager>()
            ?.FindDescendantOfType<OverlayLayer>()
            ?.FindDescendantOfType<DialogHost>()
            ?.FindDescendantOfType<LayoutTransformControl>()
            ?.FindDescendantOfType<VisualLayerManager>()
            ?.FindDescendantOfType<BetterContentDialog>();
    }

    internal static IEnumerable<BetterContentDialog> EnumerateWindowDialogs(Visual window)
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

    internal async Task<(BetterContentDialog, T)> WaitForDialog<T>(Visual window)
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

    internal void SaveScreenshot(Visual visual)
    {
        var rect = new Rect(visual!.Bounds.Size);

        var pixelSize = new PixelSize((int)rect.Width, (int)rect.Height);
        var dpiVector = new Vector(96, 96);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);

        using var fs = File.Open(
            $"C:\\StabilityMatrix\\StabilityMatrix-avalonia\\StabilityMatrix.UITests\\"
                + Guid.NewGuid().ToString()
                + ".png",
            FileMode.OpenOrCreate
        );
        bitmap.Render(visual);
        bitmap.Save(fs);
        fs.Flush();
    }
}
