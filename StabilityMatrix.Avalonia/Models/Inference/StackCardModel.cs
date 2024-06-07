using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;

public class StackCardModel
{
    public List<JsonObject>? Cards { get; init; }
}
