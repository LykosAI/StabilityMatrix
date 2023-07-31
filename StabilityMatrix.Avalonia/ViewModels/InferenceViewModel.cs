using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InferencePage))]
public partial class InferenceViewModel : PageViewModelBase
{
    private readonly ServiceManager<ViewModelBase> vmFactory;
    
    public override string Title => "Inference";
    public override IconSource IconSource => new SymbolIconSource
        {Symbol = Symbol.AppGeneric, IsFilled = true};
    
    public AvaloniaList<ViewModelBase> Tabs { get; } = new();

    public InferenceViewModel(ServiceManager<ViewModelBase> vmFactory)
    {
        this.vmFactory = vmFactory;
    }

    public override void OnLoaded()
    {
        if (Tabs.Count == 0)
        {
            Tabs.Add(vmFactory.Get<InferenceTextToImageViewModel>());
        }
        base.OnLoaded();
    }

    [RelayCommand]
    private void AddTab()
    {
        Tabs.Add(vmFactory.Get<InferenceTextToImageViewModel>());
    }
}
