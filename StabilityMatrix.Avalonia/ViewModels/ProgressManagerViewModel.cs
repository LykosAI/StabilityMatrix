using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Python.Runtime;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ProgressManagerPage))]
public partial class ProgressManagerViewModel : PageViewModelBase
{
    public override string Title => "Download Manager";
    public override Symbol Icon => Symbol.CloudDownload;

    [ObservableProperty]
    private ObservableDictionary<Guid, ProgressItem> progressItems;
    
    public ProgressManagerViewModel()
    {
        ProgressItems = new ObservableDictionary<Guid, ProgressItem>();
    }

    public void StartEventListener()
    {
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        ProgressItems[e.ProgressId] = e;
    }
}
