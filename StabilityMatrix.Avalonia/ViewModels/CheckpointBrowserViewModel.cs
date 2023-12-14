using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(CheckpointBrowserPage))]
[Singleton]
public partial class CheckpointBrowserViewModel(
    CivitAiBrowserViewModel civitAiBrowserViewModel,
    HuggingFacePageViewModel huggingFaceViewModel
) : PageViewModelBase
{
    public override string Title => "Model Browser";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.BrainCircuit, IsFilled = true };

    public IReadOnlyList<TabItem> Pages { get; } =
        new List<TabItem>(
            new List<TabViewModelBase>([civitAiBrowserViewModel, huggingFaceViewModel]).Select(
                vm => new TabItem { Header = vm.Header, Content = vm }
            )
        );
}
