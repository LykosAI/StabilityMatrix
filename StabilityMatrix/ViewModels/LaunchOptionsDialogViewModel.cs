using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class LaunchOptionsDialogViewModel : ObservableObject
{
    public ObservableCollection<LaunchOptionCard> Cards { get; set; } = new();

    [ObservableProperty]
    private string title = "Launch Options";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private string searchText = string.Empty;
    
    [ObservableProperty]
    private bool isSearchBoxEnabled = true;
    
    private LRUCache<string, ImmutableList<LaunchOptionCard>> cache = new(100);
    
    /// <summary>
    /// Return cards that match the search text
    /// </summary>
    public IEnumerable<LaunchOptionCard> FilteredCards
    {
        get
        {
            var text = SearchText;
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            {
                return Cards;
            }
            // Try cache
            if (cache.Get(text, out var cachedCards))
            {
                return cachedCards!;
            }
            var searchCard = new LaunchOptionCard(text.ToLowerInvariant());
            var extracted = FuzzySharp.Process
                .ExtractTop(searchCard, Cards, c => c.Title.ToLowerInvariant());
            var results = extracted
                .Where(r => r.Score > 40)
                .Select(r => r.Value)
                .ToImmutableList();
            cache.Add(text, results);
            return results;
        }
    }

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
    
    /// <summary>
    /// Clear Cards and cache
    /// </summary>
    public void Clear()
    {
        cache = new LRUCache<string, ImmutableList<LaunchOptionCard>>(100);
        Cards.Clear();
    }
    
    public void OnLoad()
    {
        Debug.WriteLine("In LaunchOptions OnLoad");
    }
}
