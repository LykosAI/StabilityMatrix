using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ProgressManagerPage))]
public partial class ProgressManagerViewModel : PageViewModelBase
{
    public override string Title => "Download Manager";
    public override IconSource IconSource => new SymbolIconSource {Symbol = Symbol.ArrowCircleDown, IsFilled = true};

    public AvaloniaList<ProgressItemViewModelBase> ProgressItems { get; } = new();

    public ProgressManagerViewModel(ITrackedDownloadService trackedDownloadService)
    {
        // Attach to the event
        trackedDownloadService.DownloadAdded += TrackedDownloadService_OnDownloadAdded;
    }
    
    private void TrackedDownloadService_OnDownloadAdded(object? sender, TrackedDownload e)
    {
        var vm = new DownloadProgressItemViewModel(e);
        ProgressItems.Add(vm);
    }
    
    public void StartEventListener()
    {
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    public void ClearDownloads()
    {
        ProgressItems.RemoveAll(ProgressItems.Where(x => x.IsCompleted));
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        if (ProgressItems.Any(x => x.Id == e.ProgressId))
            return;

        ProgressItems.Add(new ProgressItemViewModel(e));
    }
}
