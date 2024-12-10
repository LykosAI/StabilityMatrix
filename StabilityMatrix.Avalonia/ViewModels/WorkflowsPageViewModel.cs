using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(WorkflowsPage))]
[RegisterSingleton<WorkflowsPageViewModel>]
public partial class WorkflowsPageViewModel : PageViewModelBase
{
    public override string Title => Resources.Label_Workflows;
    public override IconSource IconSource => new FASymbolIconSource { Symbol = "fa-solid fa-circle-nodes" };

    public IReadOnlyList<TabItem> Pages { get; }

    [ObservableProperty]
    private TabItem? selectedPage;

    /// <inheritdoc/>
    public WorkflowsPageViewModel(
        OpenArtBrowserViewModel openArtBrowserViewModel,
        InstalledWorkflowsViewModel installedWorkflowsViewModel
    )
    {
        Pages = new List<TabItem>(
            new List<TabViewModelBase>([openArtBrowserViewModel, installedWorkflowsViewModel]).Select(
                vm => new TabItem { Header = vm.Header, Content = vm }
            )
        );
        SelectedPage = Pages.FirstOrDefault();
    }
}
