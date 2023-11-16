using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Model for view states of inference tabs
/// </summary>
[JsonSerializable(typeof(ViewState))]
public class ViewState
{
    public string? DockLayout { get; set; }
}
