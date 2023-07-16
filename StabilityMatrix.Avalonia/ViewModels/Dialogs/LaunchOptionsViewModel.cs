using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FuzzySharp;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(LaunchOptionsDialog))]
public partial class LaunchOptionsViewModel : ContentDialogViewModelBase
{
    private readonly LRUCache<string, ImmutableList<LaunchOptionCard>> cache = new(100);
    
    [ObservableProperty] private string title = "Launch Options";
    [ObservableProperty] private bool isSearchBoxEnabled = true;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private string searchText = string.Empty;
    
    [ObservableProperty] 
    private IReadOnlyList<LaunchOptionCard>? filteredCards;
    
    public IReadOnlyList<LaunchOptionCard>? Cards { get; set; }
    
    /// <summary>
    /// Return cards that match the search text
    /// </summary>
    private IReadOnlyList<LaunchOptionCard>? GetFilteredCards()
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

        var searchCard = new LaunchOptionCard
        {
            Title = text.ToLowerInvariant(),
            Type = LaunchOptionType.Bool,
            Options = Array.Empty<LaunchOption>()
        };
            
        var extracted = Process
            .ExtractTop(searchCard, Cards, c => c.Title.ToLowerInvariant());
        var results = extracted
            .Where(r => r.Score > 40)
            .Select(r => r.Value)
            .ToImmutableList();
        cache.Add(text, results);
        return results;
    }

    public void UpdateFilterCards() => FilteredCards = GetFilteredCards();

    public override void OnLoaded()
    {
        base.OnLoaded();
        UpdateFilterCards();
    }

    /// <summary>
    /// Export the current cards options to a list of strings
    /// </summary>
    public List<LaunchOption> AsLaunchArgs()
    {
        var launchArgs = new List<LaunchOption>();
        if (Cards is null) return launchArgs;
        
        foreach (var card in Cards)
        {
            launchArgs.AddRange(card.Options);
        }
        return launchArgs;
    }
}
