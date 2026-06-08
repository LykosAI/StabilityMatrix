using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.Views;

namespace StabilityMatrix.UITests.ModelBrowser;

[Collection("TempDir")]
public class CivArchiveBrowserTests : TestBase
{
    [AvaloniaFact]
    public async Task CivArchiveTab_ShouldLoadAndAppendResults()
    {
        var (window, _) = GetMainWindow();
        await DoInitialSetup();

        var navigationService = Services.GetRequiredService<INavigationService<MainWindowViewModel>>();
        navigationService.NavigateTo<CheckpointBrowserViewModel>();

        var frame = window.FindControl<Frame>("FrameView");
        var page = Assert.IsType<CheckpointBrowserPage>(
            await WaitHelper.WaitForConditionAsync(
                () => frame!.Content,
                content => content is CheckpointBrowserPage
            )
        );
        var vm = Assert.IsType<CheckpointBrowserViewModel>(page.DataContext);

        var civArchiveTab = vm.Pages.First(item => Equals(item.Header, "CivArchive"));
        vm.SelectedPage = civArchiveTab;
        var civArchiveVm = Assert.IsType<CivArchiveBrowserViewModel>(civArchiveTab.Content);
        civArchiveVm.OnLoaded();

        await WaitHelper.WaitForConditionAsync(() => civArchiveVm.Results.Count, count => count == 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, civArchiveVm.Results.Count);

        await civArchiveVm.LoadNextPageAsync();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(4, civArchiveVm.Results.Count);
    }
}
