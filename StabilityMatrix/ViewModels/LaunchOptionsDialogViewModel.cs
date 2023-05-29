using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class LaunchOptionsDialogViewModel : ObservableObject
{
    public ObservableCollection<LaunchOptionCard> Cards { get; set; } = new();

    [ObservableProperty]
    private BasePackage? selectedPackage;
    
    public void OnLoad()
    {
        // Populate Cards using the selected package
        
        
    }
}
