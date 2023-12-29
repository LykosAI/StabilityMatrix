using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.UITests.Extensions;

namespace StabilityMatrix.UITests.ModelBrowser;

[UsesVerify]
[Collection("TempDir")]
public class CivitAiBrowserTests : TestBase
{
    [AvaloniaFact]
    public async Task NavigateToModelBrowser_ShouldWork()
    {
        var (window, viewModel) = GetMainWindow();
        await DoInitialSetup();

        var y = window
            .FindDescendantOfType<NavigationView>()
            .GetVisualDescendants()
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Content.ToString() == "Model Browser");

        await window.ClickTargetAsync(y);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        var frame = window.FindControl<Frame>("FrameView");
        Assert.IsType<CheckpointBrowserPage>(frame.Content);

        await Task.Delay(1000);
        await Verify(window, Settings);
    }
}
