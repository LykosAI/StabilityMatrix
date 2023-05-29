using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using ABI.Windows.Data.Xml.Dom;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models;

public class LaunchOptionCard
{
    public string Title { get; set; }
    public string? Description { get; set; }
    public ObservableCollection<LaunchOption> Options { get; set; } = new();
    
    public LaunchOptionCard(LaunchOptionDefinition definition)
    {
        Title = definition.Name;
        foreach (var optionName in definition.Options)
        {
            var option = new LaunchOption {Name = optionName};
            Options.Add(option);
        }
    }
}
