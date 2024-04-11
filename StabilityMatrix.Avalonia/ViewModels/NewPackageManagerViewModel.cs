using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(NewPackageManagerPage))]
[Singleton]
public partial class NewPackageManagerViewModel : PageViewModelBase
{
    public override string Title => Resources.Label_Packages;
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true };

    public IReadOnlyList<PageViewModelBase> SubPages { get; }

    [ObservableProperty]
    private ObservableCollection<PageViewModelBase> currentPagePath = [];

    [ObservableProperty]
    private PageViewModelBase? currentPage;

    public NewPackageManagerViewModel(ServiceManager<ViewModelBase> vmFactory)
    {
        SubPages = new PageViewModelBase[]
        {
            vmFactory.Get<PackageManagerViewModel>(),
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

        if (value is PackageManagerViewModel)
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
