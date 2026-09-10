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
public abstract class AnalyticsRequest
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
