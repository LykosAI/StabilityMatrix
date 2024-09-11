namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class LaunchAnalyticsRequest : AnalyticsRequest
{
    public string? Version { get; set; }

    public string? RuntimeIdentifier { get; set; }

    public string? OsDescription { get; set; }

    public override string Type { get; set; } = "launch";
}
