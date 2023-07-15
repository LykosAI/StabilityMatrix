using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using FluentIcons.FluentAvalonia;
using Python.Runtime;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ProgressManagerPage))]
public partial class ProgressManagerViewModel : PageViewModelBase
{
    public override string Title => "Download Manager";
    public override IconSource IconSource => new SymbolIconSource {Symbol = Symbol.ArrowCircleDown, IsFilled = true};

    [ObservableProperty]
    private ObservableCollection<ProgressItemViewModel> progressItems;
    
    public ProgressManagerViewModel()
    {
        ProgressItems = new ObservableCollection<ProgressItemViewModel>();
    }

    public void StartEventListener()
    {
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    public void ClearDownloads()
    {
        if (!ProgressItems.Any(p => Math.Abs(p.Progress.Percentage - 100) < 0.01f))
            return;
        
        var itemsInProgress = ProgressItems
            .Where(p => p.Progress.Percentage < 100).ToList();
        ProgressItems = new ObservableCollection<ProgressItemViewModel>(itemsInProgress);
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        if (ProgressItems.Any(x => x.Id == e.ProgressId))
            return;

        ProgressItems.Add(new ProgressItemViewModel(e));
    }
}
