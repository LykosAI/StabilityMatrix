using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.UITests.Extensions;

namespace StabilityMatrix.UITests;

[UsesVerify]
[Collection("TempDir")]
[TestCaseOrderer("StabilityMatrix.UITests.PriorityOrderer", "StabilityMatrix.UITests")]
public class MainWindowTests : TestBase
{
    [AvaloniaFact, TestPriority(1)]
    public async Task MainWindow_ShouldOpen()
    {
        var (window, _) = GetMainWindow();

        window.Show();

        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // Find the select data directory dialog
        var selectDataDirectoryDialog = await WaitHelper.WaitForNotNullAsync(() => GetWindowDialog(window));
        Assert.NotNull(selectDataDirectoryDialog);

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

        await Task.Delay(1000);
        await Verify(window, Settings);
    }

    [AvaloniaFact, TestPriority(2)]
    public async Task MainWindowViewModel_ShouldOk()
    {
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        await Verify(viewModel, Settings);
    }
}
