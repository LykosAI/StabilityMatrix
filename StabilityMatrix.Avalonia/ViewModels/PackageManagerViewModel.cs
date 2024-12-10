using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using Injectio.Attributes;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(PackageManagerPage))]
[RegisterSingleton<PackageManagerViewModel>]
public partial class PackageManagerViewModel : PageViewModelBase
{
    public override string Title => Resources.Label_Packages;
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Box, IconVariant = IconVariant.Filled };

    public IReadOnlyList<PageViewModelBase> SubPages { get; }

    [ObservableProperty]
    private ObservableCollection<PageViewModelBase> currentPagePath = [];

    [ObservableProperty]
    private PageViewModelBase? currentPage;

    public PackageManagerViewModel(ServiceManager<ViewModelBase> vmFactory)
    {
        SubPages = new PageViewModelBase[]
        {
            vmFactory.Get<MainPackageManagerViewModel>(),
            vmFactory.Get<PackageInstallBrowserViewModel>(),
        };

        CurrentPagePath.AddRange(SubPages);

        CurrentPage = SubPages[0];
    }

    partial void OnCurrentPageChanged(PageViewModelBase? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is MainPackageManagerViewModel)
        {
            CurrentPagePath.Clear();
            CurrentPagePath.Add(value);
        }
        else if (value is PackageInstallDetailViewModel)
        {
            CurrentPagePath.Add(value);
        }
        else if (value is RunningPackageViewModel)
        {
            CurrentPagePath.Add(value);
        }
        else
        {
            CurrentPagePath.Clear();
            CurrentPagePath.AddRange(new[] { SubPages[0], value });
        }
    }
}
