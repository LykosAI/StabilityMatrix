using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InferencePage))]
public partial class InferenceViewModel : PageViewModelBase
{
    public override string Title => "Inference";
    public override IconSource IconSource => new SymbolIconSource
        {Symbol = Symbol.AppGeneric, IsFilled = true};
    
    public SeedCardViewModel SeedCardViewModel { get; init; } = new();
}
