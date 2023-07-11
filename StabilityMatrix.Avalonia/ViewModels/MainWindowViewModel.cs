using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(MainWindow))]
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    public string Greeting => "Welcome to Avalonia!";
    
    [ObservableProperty]
    private PageViewModelBase? currentPage;
    
    [ObservableProperty] 
    private object? selectedCategory;
    
    [ObservableProperty]
    private List<PageViewModelBase> pages = new();

    [ObservableProperty]
    private List<PageViewModelBase> footerPages = new();

    public MainWindowViewModel(ISettingsManager settingsManager, ServiceManager<ViewModelBase> dialogFactory)
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
    }
    
    public override async Task OnLoadedAsync()
    {
        CurrentPage = Pages.FirstOrDefault();
        SelectedCategory = Pages.FirstOrDefault();
        EventManager.Instance.PageChangeRequested += OnPageChangeRequested;
        
        if (!settingsManager.Settings.InstalledPackages.Any())
        {
            var viewModel = dialogFactory.Get<OneClickInstallViewModel>();
            var dialog = new ContentDialog
            {
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                Content = new OneClickInstallDialog
                {
                    DataContext = viewModel
                },
            };

            EventManager.Instance.OneClickInstallFinished += (_, skipped) =>
            {
                dialog.Hide();
                if (skipped) return;
                
                EventManager.Instance.OnTeachingTooltipNeeded();
            };

            await dialog.ShowAsync();
        }
    }

    private void OnPageChangeRequested(object? sender, Type e)
    {
        CurrentPage = Pages.FirstOrDefault(p => p.GetType() == e);
        SelectedCategory = Pages.FirstOrDefault(p => p.GetType() == e);
    }

    partial void OnSelectedCategoryChanged(object? value)
    {
        if (value is PageViewModelBase page)
        {
            CurrentPage = page;
        }
    }
}
