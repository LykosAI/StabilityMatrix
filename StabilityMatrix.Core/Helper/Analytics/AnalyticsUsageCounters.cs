namespace StabilityMatrix.Core.Helper.Analytics;

/// <summary>
/// Locally accumulated usage counters, one bucket per aggregated analytics category. Persisted
/// between sessions until the daily report is sent.
/// </summary>
public class AnalyticsUsageCounters
{
    public PackageUsageCounters PackageUsage { get; set; } = new();

    public InferenceUsageCounters InferenceUsage { get; set; } = new();

    public FeatureUsageCounters FeatureUsage { get; set; } = new();
}

public abstract class UsageCountersBase
{
    /// <summary>When the first event of the current period was recorded; null while empty.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    public abstract bool IsEmpty { get; }

    /// <summary>Marks the start of the period if this is the first event since the last reset.</summary>
    protected void Touch(DateTimeOffset now) => PeriodStart ??= now;

    protected static void Increment<TValue>(
        Dictionary<string, TValue> counters,
        string key,
        TValue amount,
        Func<TValue, TValue, TValue> add
    )
    {
        counters[key] = counters.TryGetValue(key, out var existing) ? add(existing, amount) : amount;
    }
}

public class PackageUsageCounters : UsageCountersBase
{
    public Dictionary<string, int> Launches { get; set; } = new();

    public Dictionary<string, long> RunSeconds { get; set; } = new();

    public override bool IsEmpty => Launches.Count == 0 && RunSeconds.Count == 0;

    public void RecordLaunch(string packageName, DateTimeOffset now)
    {
        Touch(now);
        Increment(Launches, packageName, 1, (a, b) => a + b);
    }

    public void RecordRun(string packageName, TimeSpan duration, DateTimeOffset now)
    {
        Touch(now);
        Increment(RunSeconds, packageName, (long)duration.TotalSeconds, (a, b) => a + b);
    }

    public Dictionary<string, int> GetRunMinutes() =>
        RunSeconds.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value / 60d));
}

public class InferenceUsageCounters : UsageCountersBase
{
    public Dictionary<string, int> Generations { get; set; } = new();

    public Dictionary<string, int> ModelFamilies { get; set; } = new();

    public override bool IsEmpty => Generations.Count == 0 && ModelFamilies.Count == 0;

    public void RecordGeneration(string projectType, string? modelFamily, DateTimeOffset now)
    {
        Touch(now);
        Increment(Generations, projectType, 1, (a, b) => a + b);

        if (!string.IsNullOrEmpty(modelFamily))
        {
            Increment(ModelFamilies, modelFamily, 1, (a, b) => a + b);
        }
    }
}

public class FeatureUsageCounters : UsageCountersBase
{
    public Dictionary<string, int> PageVisits { get; set; } = new();

    public override bool IsEmpty => PageVisits.Count == 0;

    public void RecordPageVisit(string pageName, DateTimeOffset now)
    {
        Touch(now);
        Increment(PageVisits, pageName, 1, (a, b) => a + b);
    }
}
