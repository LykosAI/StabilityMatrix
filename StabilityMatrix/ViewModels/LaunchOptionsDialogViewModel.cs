using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class LaunchOptionsDialogViewModel : ObservableObject
{
    public ObservableCollection<LaunchOptionCard> Cards { get; set; } = new();

    [ObservableProperty]
    private BasePackage? selectedPackage;

    /// <summary>
    /// Export the current cards options to a list of strings
    /// </summary>
    public List<string> AsLaunchArgs()
    {
        return (
            from card in Cards from option in card.Options 
            where option.Selected select option.Name).ToList();
    }
    
    /// <summary>
    /// Create cards using definitions
    /// </summary>
    public void CardsFromDefinitions(List<LaunchOptionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            Cards.Add(new LaunchOptionCard(definition));
        }
    }
    
    /// <summary>
    /// Import the current cards options from a list of strings
    /// </summary>
    public void LoadFromLaunchArgs(IEnumerable<string> launchArgs)
    {
        var launchArgsSet = new HashSet<string>(launchArgs);
        foreach (var card in Cards)
        {
            foreach (var option in card.Options)
            {
                option.Selected = launchArgsSet.Contains(option.Name);
            }
        }
    }
    
    public void OnLoad()
    {
        Debug.WriteLine("In LaunchOptions OnLoad");
    }
}
