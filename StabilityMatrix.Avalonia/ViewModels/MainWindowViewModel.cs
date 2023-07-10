using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(MainWindow))]
public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    
    [ObservableProperty]
    private PageViewModelBase? currentPage;
    
    [ObservableProperty] 
    private object? selectedCategory;
    
    [ObservableProperty]
    private List<PageViewModelBase> pages = new();

    [ObservableProperty]
    private List<PageViewModelBase> footerPages = new();
    
    public override void OnLoaded()
    {
        CurrentPage = Pages.FirstOrDefault();
        SelectedCategory = Pages.FirstOrDefault();
        EventManager.Instance.PageChangeRequested += OnPageChangeRequested;
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
