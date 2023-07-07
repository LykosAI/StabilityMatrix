using System;
using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    
    [ObservableProperty]
    private PageViewModelBase? currentPage;
    
    public List<PageViewModelBase> Pages { get; } = new();
    
    public MainWindowViewModel(LaunchPageViewModel launchPageViewModel)
    {
        Pages.Add(launchPageViewModel);
        CurrentPage = Pages[0];
    }

    public MainWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("Default Constructor is only for design-time.");
        }
        Pages.Add(new LaunchPageViewModel());
        CurrentPage = Pages[0];
    }
}
