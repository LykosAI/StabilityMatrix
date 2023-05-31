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

    /// <summary>
    /// Export the current cards options to a list of strings
    /// </summary>
    public List<LaunchOption> AsLaunchArgs()
    {
        var launchArgs = new List<LaunchOption>();
        foreach (var card in Cards)
        {
            launchArgs.AddRange(card.Options);
        }
        return launchArgs;
    }
    
    /// <summary>
    /// Create cards using definitions
    /// </summary>
    public void CardsFromDefinitions(IEnumerable<LaunchOptionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            Cards.Add(new LaunchOptionCard(definition));
        }
    }
    
    /// <summary>
    /// Import the current cards options from a list of strings
    /// </summary>
    public void LoadFromLaunchArgs(IEnumerable<LaunchOption> launchArgs)
    {
        var launchArgsDict = launchArgs.ToDictionary(launchArg => launchArg.Name);
        foreach (var card in Cards)
        {
            foreach (var option in card.Options)
            {
                var userOption = launchArgsDict.GetValueOrDefault(option.Name);
                var userValue = userOption?.OptionValue?.ToString();
                option.SetValueFromString(userValue);
            }
        }
    }
    
    public void OnLoad()
    {
        Debug.WriteLine("In LaunchOptions OnLoad");
    }
}
