using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        Debug.WriteLine("In LaunchOptions OnLoad");
        // Populate Cards using the selected package
        Cards.Clear();

        var package = SelectedPackage;
        if (package == null)
        {
            Debug.WriteLine($"selectedPackage is null");
            return;
        }
        var definitions = package.LaunchOptions;
        if (definitions == null)
        {
            Debug.WriteLine($"definitions is null");
            return;
        }
        
        foreach (var definition in definitions)
        {
            Cards.Add(new LaunchOptionCard(definition));
        }
        Debug.WriteLine($"Cards: {Cards.Count}");
    }
}
