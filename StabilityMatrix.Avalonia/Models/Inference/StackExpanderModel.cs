using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(StackExpanderModel))]
public class StackExpanderModel : StackCardModel
{
    public string? Title { get; set; }
    public bool IsEnabled { get; set; }
}
