using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;

namespace StabilityMatrix.ViewModels;

public partial class SelectInstallLocationsViewModel : ObservableObject
{
    [ObservableProperty] private string dataDirectory;
    [ObservableProperty] private bool isPortableMode;
    
    public string DefaultInstallLocation => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix");
    
    public SelectInstallLocationsViewModel()
    {
        DataDirectory = DefaultInstallLocation;
    }

    [RelayCommand]
    private void ShowFolderBrowserDialog()
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.SelectedPath;
        if (path == null) return;
        
        DataDirectory = path;
    }

    partial void OnIsPortableModeChanged(bool value)
    {
        DataDirectory = value
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data")
                : DefaultInstallLocation;
    }
}
