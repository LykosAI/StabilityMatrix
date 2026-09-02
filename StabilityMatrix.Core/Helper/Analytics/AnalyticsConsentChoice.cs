using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Helper.Analytics;

/// <summary>
/// The user's answer to the consent dialog.
/// </summary>
/// <param name="Accepted">False turns everything off regardless of the other members.</param>
/// <param name="Categories">Categories to enable when accepted.</param>
/// <param name="HardwareDetail">Detail level for the hardware category.</param>
public record AnalyticsConsentChoice(
    bool Accepted,
    IReadOnlyCollection<AnalyticsCategory> Categories,
    HardwareDetailLevel HardwareDetail
);
