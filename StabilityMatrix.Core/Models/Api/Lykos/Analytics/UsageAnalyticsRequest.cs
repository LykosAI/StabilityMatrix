namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

/// <summary>
/// Base for the daily aggregated usage documents. Counters cover the period between
/// <see cref="PeriodStart"/> and <see cref="PeriodEnd"/>; nothing finer-grained leaves the machine.
/// </summary>
public abstract class UsageAnalyticsRequest : AnalyticsRequest
{
    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }
}
