using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Settings;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
[Singleton]
public partial class SettingsViewModel : PageViewModelBase
{
    public override string Title => "Settings";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Settings, IconVariant = IconVariant.Filled };

    public IReadOnlyList<PageViewModelBase> SubPages { get; }

    [ObservableProperty]
    private ObservableCollection<PageViewModelBase> currentPagePath = [];

    [ObservableProperty]
    private PageViewModelBase? currentPage;

    public SettingsViewModel(ServiceManager<ViewModelBase> vmFactory)
    {
        SubPages = new PageViewModelBase[]
        {
            vmFactory.Get<MainSettingsViewModel>(),
            vmFactory.Get<InferenceSettingsViewModel>(),
            vmFactory.Get<AccountSettingsViewModel>(),
            vmFactory.Get<UpdateSettingsViewModel>(),
            vmFactory.Get<NotificationSettingsViewModel>()
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

        if (value is MainSettingsViewModel)
        {
            CurrentPagePath.Clear();
            CurrentPagePath.Add(value);
        }
        else
        {
            CurrentPagePath.Clear();
            CurrentPagePath.AddRange(new[] { SubPages[0], value });
        }
    }
}
