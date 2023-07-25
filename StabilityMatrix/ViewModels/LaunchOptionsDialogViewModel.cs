using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.ViewModels;

public partial class LaunchOptionsDialogViewModel : ObservableObject
{
    public static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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

    public void Initialize(IEnumerable<LaunchOptionDefinition> definitions, IEnumerable<LaunchOption> launchArgs)
    {
        Clear();
        // During card creation, store dict of options with initial values
        var initialOptions = new Dictionary<string, object>();
        // Create cards
        foreach (var definition in definitions)
        {
            // Check that non-bool types have exactly one option
            if (definition.Type != LaunchOptionType.Bool && definition.Options.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Definition: '{definition.Name}' has {definition.Options.Count} options," +
                    $" it must have exactly 1 option for non-bool types");
            }
            // Store initial values
            if (definition.InitialValue != null)
            {
                // For bool types, initial value can be string (single/multiple options) or bool (single option)
                if (definition.Type == LaunchOptionType.Bool)
                {
                    // For single option, check bool
                    if (definition.Options.Count == 1 && definition.InitialValue is bool boolValue)
                    {
                        initialOptions[definition.Options.First()] = boolValue;
                    }
                    else
                    {
                        // For single/multiple options (string only)
                        var option = definition.Options.FirstOrDefault(opt => opt.Equals(definition.InitialValue));
                        if (option == null)
                        {
                            throw new InvalidOperationException(
                                $"Definition '{definition.Name}' has InitialValue of '{definition.InitialValue}', but it was not found in options:" +
                                $" '{string.Join(",", definition.Options)}'");
                        }
                        initialOptions[option] = true;
                    }
                }
                else
                {
                    // Otherwise store initial value for first option
                    initialOptions[definition.Options.First()] = definition.InitialValue;
                }
            }
            Cards.Add(new LaunchOptionCard(definition));
        }
        // Load launch args
        var launchArgsDict = launchArgs.ToDictionary(launchArg => launchArg.Name);
        foreach (var card in Cards)
        {
            foreach (var option in card.Options)
            {
                var userOption = launchArgsDict.GetValueOrDefault(option.Name);
                var userValue = userOption?.OptionValue?.ToString();
                // If no user value, check for initial value
                if (userValue == null)
                {
                    var initialValue = initialOptions.GetValueOrDefault(option.Name);
                    if (initialValue != null)
                    {
                        userValue = initialValue.ToString();
                        Logger.Info("Using initial value '{InitialValue}' for option '{OptionName}'",
                            initialValue, option.Name);
                    }
                }
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
