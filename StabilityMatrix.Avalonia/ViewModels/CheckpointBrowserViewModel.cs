using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(CheckpointBrowserPage))]
[Singleton]
public partial class CheckpointBrowserViewModel : PageViewModelBase
{
    public override string Title => "Model Browser";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.BrainCircuit, IsFilled = true };

    public IReadOnlyList<TabItem> Pages { get; }

    [ObservableProperty]
    private TabItem? selectedPage;

    /// <inheritdoc/>
    public CheckpointBrowserViewModel(
        CivitAiBrowserViewModel civitAiBrowserViewModel,
        HuggingFacePageViewModel huggingFaceViewModel
    )
    {
        Pages = new List<TabItem>(
            new List<TabViewModelBase>([civitAiBrowserViewModel, huggingFaceViewModel]).Select(
                vm => new TabItem { Header = vm.Header, Content = vm }
            )
        );
        SelectedPage = Pages.FirstOrDefault();
        EventManager.Instance.NavigateAndFindCivitModelRequested += OnNavigateAndFindCivitModelRequested;
    }

    private void OnNavigateAndFindCivitModelRequested(object? sender, int e)
    {
        SelectedPage = Pages.FirstOrDefault();
    }
}
