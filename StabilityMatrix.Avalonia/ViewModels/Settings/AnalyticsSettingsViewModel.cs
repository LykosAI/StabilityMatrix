using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Analytics;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AnalyticsSettingsPage))]
[ManagedService]
[RegisterSingleton<AnalyticsSettingsViewModel>]
public partial class AnalyticsSettingsViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IAnalyticsHelper analyticsHelper;

    /// <summary>True while copying settings into the view model, so the copy is not written back.</summary>
    private bool isRefreshing;

    public override string Title => Resources.Label_Analytics;

    /// <inheritdoc />
    public override IconSource IconSource => new FASymbolIconSource { Symbol = @"fa-solid fa-chart-simple" };

    [ObservableProperty]
    public partial bool IsUsageDataEnabled { get; set; }

    [ObservableProperty]
    public partial string? InstallationIdDisplay { get; set; }

    public IReadOnlyList<AnalyticsCategoryViewModel> Categories { get; }

    public TextDocument LaunchExampleDocument { get; } = new(Resources.AnalyticsExample_Launch);

    public AnalyticsSettingsViewModel(ISettingsManager settingsManager, IAnalyticsHelper analyticsHelper)
    {
        this.settingsManager = settingsManager;
        this.analyticsHelper = analyticsHelper;

        Categories = AnalyticsCategories.All.Select(info => new AnalyticsCategoryViewModel(info)).ToList();

        foreach (var category in Categories)
        {
            category.PropertyChanged += OnCategoryPropertyChanged;
        }

        RefreshFromSettings();
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        RefreshFromSettings();
    }

    partial void OnIsUsageDataEnabledChanged(bool value)
    {
        if (isRefreshing)
            return;

        analyticsHelper.SetUsageDataEnabled(value);
        RefreshFromSettings();
    }

    private void OnCategoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isRefreshing || sender is not AnalyticsCategoryViewModel category)
            return;

        switch (e.PropertyName)
        {
            case nameof(AnalyticsCategoryViewModel.IsEnabled):
                analyticsHelper.SetCategoryEnabled(category.Category, category.IsEnabled);
                break;
            case nameof(AnalyticsCategoryViewModel.IsDetailed) when category.SupportsDetailLevel:
                analyticsHelper.SetHardwareDetailLevel(category.DetailLevel);
                break;
        }
    }

    [RelayCommand]
    private void ResetInstallationId()
    {
        analyticsHelper.ResetInstallationId();
        RefreshFromSettings();
    }

    private void RefreshFromSettings()
    {
        var analytics = settingsManager.Settings.Analytics;

        isRefreshing = true;
        try
        {
            IsUsageDataEnabled = analytics.IsUsageDataEnabled;
            InstallationIdDisplay = analytics.InstallationId?.ToString();

            foreach (var category in Categories)
            {
                category.IsEnabled = analytics.Categories.GetValueOrDefault(category.Category);
                category.IsDetailed = analytics.HardwareDetailLevel == HardwareDetailLevel.Detailed;
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }
}
