using System;
using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(MainWindow))]
public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    
    [ObservableProperty]
    private PageViewModelBase? currentPage;

    [ObservableProperty] 
    private object? selectedCategory;
    
    public List<PageViewModelBase> Pages { get; } = new();

    public MainWindowViewModel(LaunchPageViewModel launchPageViewModel,
        PackageManagerViewModel packageManagerViewModel)
    {
        Pages.Add(launchPageViewModel);
        Pages.Add(packageManagerViewModel);
        CurrentPage = Pages[0];
        SelectedCategory = Pages[0];
    }

    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("Default Constructor is only for design-time.");
        }
        Pages.Add(new LaunchPageViewModel());
        Pages.Add(new PackageManagerViewModel(null!, null!));
        CurrentPage = Pages[0];
        SelectedCategory = Pages[0];
    }

    partial void OnSelectedCategoryChanged(object? value)
    {
        if (value is PageViewModelBase page)
        {
            CurrentPage = page;
        }
    }
}
