using System.Collections.Generic;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Models.Analytics;

/// <summary>
/// Display metadata for one opt-in analytics category: what to call it, what it sends, and an
/// example of the document. The consent dialog and the settings page both render from this.
/// </summary>
public record AnalyticsCategoryInfo(
    AnalyticsCategory Category,
    string Title,
    string Description,
    string ExampleJson,
    Symbol Icon
)
{
    /// <summary>
    /// Example of the coarsened document for categories that offer a minimal detail level; null
    /// for categories without one.
    /// </summary>
    public string? MinimalExampleJson { get; init; }
}

public static class AnalyticsCategories
{
    /// <summary>All categories in display order, with localized text for the current culture.</summary>
    public static IReadOnlyList<AnalyticsCategoryInfo> All =>
        [
            new(
                AnalyticsCategory.Hardware,
                Resources.Label_AnalyticsHardware,
                Resources.Text_AnalyticsHardwareDescription,
                Resources.AnalyticsExample_Hardware,
                Symbol.DeveloperBoard
            )
            {
                MinimalExampleJson = Resources.AnalyticsExample_HardwareMinimal,
            },
            new(
                AnalyticsCategory.PackageUsage,
                Resources.Label_AnalyticsPackageUsage,
                Resources.Text_AnalyticsPackageUsageDescription,
                Resources.AnalyticsExample_PackageUsage,
                Symbol.Apps
            ),
            new(
                AnalyticsCategory.InferenceUsage,
                Resources.Label_AnalyticsInferenceUsage,
                Resources.Text_AnalyticsInferenceUsageDescription,
                Resources.AnalyticsExample_InferenceUsage,
                Symbol.Image
            ),
            new(
                AnalyticsCategory.FeatureUsage,
                Resources.Label_AnalyticsFeatureUsage,
                Resources.Text_AnalyticsFeatureUsageDescription,
                Resources.AnalyticsExample_FeatureUsage,
                Symbol.AppsList
            ),
            new(
                AnalyticsCategory.PackageInstalls,
                Resources.Label_AnalyticsPackageInstalls,
                Resources.Text_AnalyticsPackageInstallsDescription,
                Resources.AnalyticsExample_InstallData,
                Symbol.ArrowDownload
            ),
        ];
}
