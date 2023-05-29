using System.Collections.Generic;
using System.Windows.Documents;

namespace StabilityMatrix.Models;

public class LaunchOptionCard
{
    public string Title { get; set; }
    public string? Description { get; set; }
    public List<string> Options { get; set; }
    
    public LaunchOptionCard(LaunchOptionDefinition definition)
    {
        Title = definition.Name;
        Options = definition.Options;
    }
}
