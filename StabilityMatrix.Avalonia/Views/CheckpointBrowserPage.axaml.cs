using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<CheckpointBrowserPage>]
public partial class CheckpointBrowserPage : UserControlBase, IHandleNavigation
{
    public CheckpointBrowserPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Forward "back" to the active browser tab so a tab can handle its own internal navigation
    /// (e.g. the HuggingFace tab returning from a repo's files to the search results) before the
    /// main navigation leaves the page.
    /// </summary>
    public bool GoBack() =>
        (DataContext as CheckpointBrowserViewModel)?.SelectedPage?.Content is IHandleNavigation handler
        && handler.GoBack();
}
