using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(StackCardModel))]
public class StackCardModel
{
    public List<JsonObject>? Cards { get; init; }
}
