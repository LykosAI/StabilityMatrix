using System.Text.Json.Serialization;
using StabilityMatrix.Avalonia.ViewModels.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(StackExpanderModel))]
public class StackExpanderModel : StackCardModel
{
    public string? Title { get; set; }
    public bool IsEnabled { get; set; }
}
