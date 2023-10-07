using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(OutputsPage))]
public partial class OutputsPageViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    public override string Title => "Outputs";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Grid, IsFilled = true };

    public SourceCache<FileInfo, string> OutputsCache { get; } = new(p => p.FullName);
    public IObservableCollection<string> Outputs { get; } =
        new ObservableCollectionExtended<string>();

    public OutputsPageViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;

        OutputsCache
            .Connect()
            .DeferUntilLoaded()
            .SortBy(x => x.CreationTime, SortDirection.Descending)
            .Select(x => x.FullName)
            .Bind(Outputs)
            .Subscribe();
    }

    public override void OnLoaded()
    {
        GetOutputs();
    }

    private void GetOutputs()
    {
        if (!settingsManager.IsLibraryDirSet)
            return;

        foreach (
            var file in Directory.EnumerateFiles(
                settingsManager.OutputDirectory,
                "*.*",
                SearchOption.AllDirectories
            )
        )
        {
            var fileInfo = new FileInfo(file);
            if (!fileInfo.Extension.Contains("png"))
                continue;

            OutputsCache.AddOrUpdate(fileInfo);
        }
    }
}
