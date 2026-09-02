using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Helper.Analytics;

/// <summary>
/// Accumulates usage counters locally for the aggregated analytics categories. Every Record
/// method is a no-op unless the matching category is enabled, so nothing builds up for data the
/// user has not opted into.
/// </summary>
public interface IAnalyticsUsageTracker
{
    void RecordPackageLaunch(string packageName);

    void RecordPackageRun(string packageName, TimeSpan duration);

    /// <param name="projectType">Inference tab type name, e.g. "TextToImage".</param>
    void RecordInferenceTabOpened(string projectType);

    /// <param name="projectType">Inference tab type name, e.g. "TextToImage".</param>
    void RecordInferenceGeneration(string projectType);

    void RecordPageVisit(string pageName);

    /// <summary>
    /// Builds one report per enabled category that has data, covering up to
    /// <paramref name="periodEnd"/>. Counters are kept until <see cref="ResetCategory"/> is
    /// called, so a failed send is retried with the next report.
    /// </summary>
    IReadOnlyList<(AnalyticsCategory Category, UsageAnalyticsRequest Request)> BuildReports(
        DateTimeOffset periodEnd
    );

    /// <summary>Clears the counters of a category after its report was delivered.</summary>
    void ResetCategory(AnalyticsCategory category);
}
