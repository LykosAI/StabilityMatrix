using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

/// <summary>
/// Envelope shared by every analytics document. The concrete type is written as the
/// <c>type</c> discriminator; registering a new document means adding one
/// <see cref="JsonDerivedTypeAttribute"/> here.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LaunchAnalyticsRequest), "launch")]
[JsonDerivedType(typeof(PackageInstallAnalyticsRequest), "package-install")]
[JsonDerivedType(typeof(FirstTimeInstallAnalytics), "first-time-install")]
[JsonDerivedType(typeof(HardwareAnalyticsRequest), "hardware")]
[JsonDerivedType(typeof(PackageUsageAnalyticsRequest), "package-usage")]
[JsonDerivedType(typeof(InferenceUsageAnalyticsRequest), "inference-usage")]
[JsonDerivedType(typeof(FeatureUsageAnalyticsRequest), "feature-usage")]
public abstract class AnalyticsRequest
{
    /// <summary>
    /// Random per-installation identifier, stamped on every request while usage data is enabled.
    /// </summary>
    public Guid? InstallationId { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
