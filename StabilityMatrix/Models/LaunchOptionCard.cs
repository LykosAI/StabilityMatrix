using System.Collections.ObjectModel;

namespace StabilityMatrix.Models;

public class LaunchOptionCard
{
    public string Title { get; set; }
    public LaunchOptionType Type { get; set; }
    public string? Description { get; set; }
    public ObservableCollection<LaunchOption> Options { get; set; } = new();

    public LaunchOptionCard(string title, LaunchOptionType type = LaunchOptionType.Bool)
    {
        Title = title;
        Type = type;
    }
    
    public LaunchOptionCard(LaunchOptionDefinition definition)
    {
        Title = definition.Name;
        Description = definition.Description;
        Type = definition.Type;
        foreach (var optionName in definition.Options)
        {
            var option = new LaunchOption
            {
                Name = optionName,
                Type = definition.Type,
                DefaultValue = definition.DefaultValue
            };
            Options.Add(option);
        }
    }
}
