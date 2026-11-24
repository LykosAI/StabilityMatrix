namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// A user-defined model category. Only presentation lives here; which models belong to the
/// category is stored per model in <see cref="UserModelMetadata"/>.
/// </summary>
public class ModelCategory
{
    public required string Name { get; set; }

    /// <summary>
    /// Optional display colour as a hex string (e.g. <c>#D9930D</c>). Null means "use the default chip colour".
    /// </summary>
    public string? Color { get; set; }
}
