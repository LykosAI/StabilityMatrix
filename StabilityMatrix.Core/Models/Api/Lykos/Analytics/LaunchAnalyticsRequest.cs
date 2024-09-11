namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class LaunchAnalyticsRequest : AnalyticsRequest
{
    public string? Version { get; set; }

    public string? RuntimeIdentifier { get; set; }

    public string? OsName { get; set; }

    public string? OsVersion { get; set; }

    public override string Type { get; set; } = "launch";
}
