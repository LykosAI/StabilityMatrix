using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class OpenModelDbBrowserPage : UserControlBase
{
    public OpenModelDbBrowserPage()
    {
        InitializeComponent();
    }
    /*private readonly ISettingsManager settingsManager;

    public OpenModelDbBrowserPage(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        InitializeComponent();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (scrollViewer.Offset.Y == 0)
            return;

        var isAtEnd = Math.Abs(scrollViewer.Offset.Y - scrollViewer.ScrollBarMaximum.Y) < 1f;

        if (
            isAtEnd
            && settingsManager.Settings.IsWorkflowInfiniteScrollEnabled
            && DataContext is IInfinitelyScroll scroll
        )
        {
            scroll.LoadNextPageAsync().SafeFireAndForget();
        }
    }*/
}
