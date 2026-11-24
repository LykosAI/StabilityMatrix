using System.Text.Json;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnalyticsSettingsTests
{
    [TestMethod]
    public void NewSettings_LeaveAnalyticsDisabled()
    {
        var settings = new AnalyticsSettings();
        Assert.IsFalse(settings.IsUsageDataEnabled);
        Assert.IsNull(settings.LastSeenConsentAccepted);
        Assert.IsNull(settings.LastSeenConsentVersion);
        Assert.AreEqual(TimeSpan.FromDays(1), AnalyticsSettings.DefaultLaunchDataSendInterval);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExpandedSettings_PreserveConsentAndDiscardExpandedFields(bool enabled)
    {
        var json = $$"""
            {
                "LastSeenConsentVersion": "2.17.0",
                "LastSeenConsentAccepted": {{enabled.ToString().ToLowerInvariant()}},
                "IsUsageDataEnabled": {{enabled.ToString().ToLowerInvariant()}},
                "LaunchDataLastSentAt": "2026-09-01T14:02:11+00:00",
                "LastSeenConsentRevision": 2,
                "InstallationId": "3f1c9b2e-7a44-4d8b-9c1e-5e2f8a6b0d21",
                "Categories": { "Hardware": true, "PackageUsage": true },
                "HardwareDetailLevel": 1
            }
            """;

        var settings = JsonSerializer.Deserialize<AnalyticsSettings>(json)!;

        Assert.AreEqual(enabled, settings.IsUsageDataEnabled);
        Assert.AreEqual(enabled, settings.LastSeenConsentAccepted);
        Assert.AreEqual("2.17.0", settings.LastSeenConsentVersion!.ToString());
        Assert.AreEqual(DateTimeOffset.Parse("2026-09-01T14:02:11+00:00"), settings.LaunchDataLastSentAt);
        var saved = JsonSerializer.Serialize(settings);
        Assert.IsFalse(saved.Contains("InstallationId"));
        Assert.IsFalse(saved.Contains("Categories"));
        Assert.IsFalse(saved.Contains("HardwareDetailLevel"));
        Assert.IsFalse(saved.Contains("LastSeenConsentRevision"));
    }
}
