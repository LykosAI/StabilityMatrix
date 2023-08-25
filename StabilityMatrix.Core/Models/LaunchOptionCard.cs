using System.Collections.Immutable;
using System.Diagnostics;

namespace StabilityMatrix.Core.Models;

public readonly record struct LaunchOptionCard
{
    public required string Title { get; init; }
    public required LaunchOptionType Type { get; init; }
    public required IReadOnlyList<LaunchOption> Options { get; init; }
    public string? Description { get; init; }
    
    public static LaunchOptionCard FromDefinition(LaunchOptionDefinition definition)
    {
        return new LaunchOptionCard
        {
            Title = definition.Name,
            Description = definition.Description,
            Type = definition.Type,
    
            Options = definition.Options.Select(s =>
            {
                var option = new LaunchOption
                {
                    Name = s,
                    Type = definition.Type,
                    DefaultValue = definition.DefaultValue
                };
                return option;
            }).ToImmutableArray()
        };
    }
    
    /// <summary>
    /// Yield LaunchOptionCards given definitions and launch args to load
    /// </summary>
    /// <param name="definitions"></param>
    /// <param name="launchArgs"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IEnumerable<LaunchOptionCard> FromDefinitions(
        IEnumerable<LaunchOptionDefinition> definitions, 
        IEnumerable<LaunchOption> launchArgs)
    {
        // During card creation, store dict of options with initial values
        var initialOptions = new Dictionary<string, object>();
        
        // To dictionary ignoring duplicates
        var launchArgsDict = launchArgs
            .ToLookup(launchArg => launchArg.Name)
            .ToDictionary(
                group => group.Key, 
                group => group.First()
            );
        
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
            // Create the new card
            var card = new LaunchOptionCard
            {
                Title = definition.Name,
                Description = definition.Description,
                Type = definition.Type,
                Options = definition.Options.Select(s =>
                {
                    // Parse defaults and user loaded values here
                    var userOption = launchArgsDict.GetValueOrDefault(s);
                    var userValue = userOption?.OptionValue;
                    // If no user value, check set initial value
                    if (userValue is null)
                    {
                        var initialValue = initialOptions.GetValueOrDefault(s);
                        userValue ??= initialValue;
                        Debug.WriteLineIf(initialValue != null, 
                            $"Using initial value {initialValue} for option {s}");
                    }
                    
                    var option = new LaunchOption
                    {
                        Name = s,
                        Type = definition.Type,
                        DefaultValue = definition.DefaultValue,
                        OptionValue = userValue
                    };
                    return option;
                }).ToImmutableArray()
            };
            
            yield return card;
        }
    }
}
