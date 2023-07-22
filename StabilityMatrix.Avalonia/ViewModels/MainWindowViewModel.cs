using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
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

    public ProgressManagerViewModel ProgressManagerViewModel { get; init; }
    public UpdateViewModel UpdateViewModel { get; init; }

    public MainWindowViewModel(ISettingsManager settingsManager, ServiceManager<ViewModelBase> dialogFactory)
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        
        ProgressManagerViewModel = dialogFactory.Get<ProgressManagerViewModel>();
        UpdateViewModel = dialogFactory.Get<UpdateViewModel>();
    }
    
    public override async Task OnLoadedAsync()
    {
        CurrentPage = Pages.FirstOrDefault();
        SelectedCategory = Pages.FirstOrDefault();
        EventManager.Instance.PageChangeRequested += OnPageChangeRequested;

        await EnsureDataDirectory();
        
        // Index checkpoints if we dont have
        settingsManager.IndexCheckpoints();
        
        if (!settingsManager.Settings.InstalledPackages.Any())
        {
            var viewModel = dialogFactory.Get<OneClickInstallViewModel>();
            var dialog = new BetterContentDialog
            {
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                IsFooterVisible = false,
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

    /// <summary>
    /// Check if the data directory exists, if not, show the select data directory dialog.
    /// </summary>
    private async Task EnsureDataDirectory()
    {
        // Show dialog if not set
        if (!settingsManager.TryFindLibrary())
        {
            await ShowSelectDataDirectoryDialog();
        }
        
        // Try to find library again, should be found now
        if (!settingsManager.TryFindLibrary())
        {
            throw new Exception("Could not find library after setting path");
        }
        
        // Tell LaunchPage to load any packages if they selected an existing directory
        EventManager.Instance.OnInstalledPackagesChanged();
        
        // Check if there are old packages, if so show migration dialog
        // TODO: Migration dialog
    }
    
    private async Task ShowSelectDataDirectoryDialog()
    {
        var viewModel = dialogFactory.Get<SelectDataDirectoryViewModel>();
        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new SelectDataDirectoryDialog
            {
                DataContext = viewModel
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            App.Shutdown();
        }
        
        // 1. For portable mode, call settings.SetPortableMode()
        if (viewModel.IsPortableMode)
        {
            settingsManager.SetPortableMode();
        }
        // 2. For custom path, call settings.SetLibraryPath(path)
        else
        {
            settingsManager.SetLibraryPath(viewModel.DataDirectory);
        }
    }

    public async Task ShowUpdateDialog()
    {
        var viewModel = dialogFactory.Get<UpdateViewModel>();
        var dialog = new BetterContentDialog
        {
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new UpdateDialog
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowAsync();
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
