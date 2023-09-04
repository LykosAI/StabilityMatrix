using System.Text.Json.Nodes;
using LiteDB;

namespace StabilityMatrix.Core.Models.Database;

public record InferenceProjectEntry
{
    [BsonId]
    public required Guid Id { get; init; }

    /// <summary>
    /// Full path to the project file (.smproj)
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Whether the project is open in the editor
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Whether the project is selected in the editor
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Current index of the tab
    /// </summary>
    public int CurrentTabIndex { get; set; } = -1;

    /// <summary>
    /// The current dock layout state
    /// </summary>
    public JsonObject? DockLayout { get; set; }
}
