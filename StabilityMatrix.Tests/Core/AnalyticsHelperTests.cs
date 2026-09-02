using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnalyticsHelperTests
{
    private Settings settings = null!;
    private ISettingsManager settingsManager = null!;
    private ILykosAnalyticsApi api = null!;
    private IAnalyticsUsageTracker usageTracker = null!;
    private List<AnalyticsRequest> sent = null!;

    [TestInitialize]
    public void Setup()
    {
        settings = new Settings();

        settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(settings);
        settingsManager
            .When(x => x.Transaction(Arg.Any<Action<Settings>>(), Arg.Any<bool>()))
            .Do(call => call.Arg<Action<Settings>>()(settings));

        sent = [];
        api = Substitute.For<ILykosAnalyticsApi>();
        api.PostInstallData(Arg.Do<AnalyticsRequest>(sent.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        usageTracker = Substitute.For<IAnalyticsUsageTracker>();
        usageTracker.BuildReports(Arg.Any<DateTimeOffset>()).Returns([]);
    }

    private AnalyticsHelper CreateHelper() =>
        new(Substitute.For<ILogger<AnalyticsHelper>>(), api, settingsManager, usageTracker);

    private void OptIn(params AnalyticsCategory[] categories)
    {
        settings.Analytics.IsUsageDataEnabled = true;
        settings.Analytics.InstallationId = Guid.NewGuid();
        foreach (var category in categories)
        {
            settings.Analytics.SetCategoryFlag(category, true);
        }
    }

    [TestMethod]
    public void ApplyConsent_Accepted_EnablesChosenCategoriesAndCreatesId()
    {
        CreateHelper().ApplyConsent(true, [AnalyticsCategory.Hardware, AnalyticsCategory.PackageInstalls]);

        var analytics = settings.Analytics;
        Assert.IsTrue(analytics.IsUsageDataEnabled);
        Assert.IsTrue(analytics.LastSeenConsentAccepted);
        Assert.AreEqual(AnalyticsSettings.CurrentConsentRevision, analytics.LastSeenConsentRevision);
        Assert.IsNotNull(analytics.LastSeenConsentVersion);
        Assert.IsNotNull(analytics.InstallationId);
        Assert.IsTrue(analytics.IsHardwareDataEnabled);
        Assert.IsTrue(analytics.IsPackageInstallDataEnabled);
        Assert.IsFalse(analytics.IsPackageUsageDataEnabled);
        Assert.IsFalse(analytics.IsInferenceUsageDataEnabled);
        Assert.IsFalse(analytics.IsFeatureUsageDataEnabled);
        Assert.IsFalse(analytics.IsConsentUnanswered);
        Assert.IsFalse(analytics.IsConsentOutdated);
    }

    [TestMethod]
    public void ApplyConsent_Accepted_KeepsExistingId()
    {
        var existing = Guid.NewGuid();
        settings.Analytics.InstallationId = existing;

        CreateHelper().ApplyConsent(true, [AnalyticsCategory.Hardware]);

        Assert.AreEqual(existing, settings.Analytics.InstallationId);
    }

    [TestMethod]
    public void ApplyConsent_Declined_ClearsEverything()
    {
        OptIn(AnalyticsCategory.Hardware, AnalyticsCategory.FeatureUsage);

        CreateHelper().ApplyConsent(false, [AnalyticsCategory.Hardware]);

        var analytics = settings.Analytics;
        Assert.IsFalse(analytics.IsUsageDataEnabled);
        Assert.IsFalse(analytics.LastSeenConsentAccepted);
        Assert.IsNull(analytics.InstallationId);
        Assert.IsFalse(analytics.IsHardwareDataEnabled);
        Assert.IsFalse(analytics.IsFeatureUsageDataEnabled);
        Assert.IsFalse(analytics.IsConsentOutdated);
    }

    [TestMethod]
    public void SetUsageDataEnabled_RotatesInstallationId()
    {
        OptIn();
        var original = settings.Analytics.InstallationId;
        var helper = CreateHelper();

        helper.SetUsageDataEnabled(false);
        Assert.IsFalse(settings.Analytics.IsUsageDataEnabled);
        Assert.IsNull(settings.Analytics.InstallationId);

        helper.SetUsageDataEnabled(true);
        Assert.IsTrue(settings.Analytics.IsUsageDataEnabled);
        Assert.IsNotNull(settings.Analytics.InstallationId);
        Assert.AreNotEqual(original, settings.Analytics.InstallationId);
    }

    [TestMethod]
    public void ResetInstallationId_IsNoOpWhileDisabled()
    {
        CreateHelper().ResetInstallationId();

        Assert.IsNull(settings.Analytics.InstallationId);
    }

    [TestMethod]
    public async Task TrackPackageInstall_RequiresPackageInstallsCategory()
    {
        OptIn();
        var helper = CreateHelper();

        await helper.TrackPackageInstallAsync("ComfyUI", "1.0", true);
        Assert.AreEqual(0, sent.Count);

        settings.Analytics.IsPackageInstallDataEnabled = true;
        await helper.TrackPackageInstallAsync("ComfyUI", "1.0", true);

        Assert.AreEqual(1, sent.Count);
        Assert.AreEqual("package-install", sent[0].Type);
        Assert.AreEqual(settings.Analytics.InstallationId, sent[0].InstallationId);
    }

    [TestMethod]
    public async Task Track_CreatesInstallationIdForLegacyOptIns()
    {
        settings.Analytics.IsUsageDataEnabled = true;

        await CreateHelper().TrackAsync(new LaunchAnalyticsRequest());

        Assert.IsNotNull(settings.Analytics.InstallationId);
        Assert.AreEqual(settings.Analytics.InstallationId, sent.Single().InstallationId);
    }

    [TestMethod]
    public async Task SendPeriodicReports_SendsLaunchAndUsageWhenDue()
    {
        OptIn(AnalyticsCategory.PackageUsage);
        var usageReport = new PackageUsageAnalyticsRequest();
        usageTracker
            .BuildReports(Arg.Any<DateTimeOffset>())
            .Returns([(AnalyticsCategory.PackageUsage, usageReport)]);

        await CreateHelper().SendPeriodicReportsIfDueAsync();

        CollectionAssert.AreEqual(new[] { "launch", "package-usage" }, sent.Select(r => r.Type).ToArray());
        Assert.IsTrue(sent.All(r => r.InstallationId == settings.Analytics.InstallationId));
        Assert.IsNotNull(settings.Analytics.LaunchDataLastSentAt);
        usageTracker.Received(1).ResetCategory(AnalyticsCategory.PackageUsage);
    }

    [TestMethod]
    public async Task SendPeriodicReports_SkipsWhenSentRecently()
    {
        OptIn(AnalyticsCategory.PackageUsage);
        settings.Analytics.LaunchDataLastSentAt = DateTimeOffset.UtcNow.AddHours(-1);

        await CreateHelper().SendPeriodicReportsIfDueAsync();

        Assert.AreEqual(0, sent.Count);
        usageTracker.DidNotReceive().BuildReports(Arg.Any<DateTimeOffset>());
    }

    [TestMethod]
    public async Task SendPeriodicReports_SkipsWhenDisabled()
    {
        await CreateHelper().SendPeriodicReportsIfDueAsync();

        Assert.AreEqual(0, sent.Count);
    }

    [TestMethod]
    public async Task SendPeriodicReports_KeepsCountersWhenLaunchPingFails()
    {
        OptIn(AnalyticsCategory.PackageUsage);
        api.PostInstallData(Arg.Any<AnalyticsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        await CreateHelper().SendPeriodicReportsIfDueAsync();

        Assert.IsNull(settings.Analytics.LaunchDataLastSentAt);
        usageTracker.DidNotReceive().BuildReports(Arg.Any<DateTimeOffset>());
        usageTracker.DidNotReceive().ResetCategory(Arg.Any<AnalyticsCategory>());
    }

    [TestMethod]
    public async Task SendPeriodicReports_KeepsCountersOfFailedUsageReport()
    {
        OptIn(AnalyticsCategory.PackageUsage);
        var usageReport = new PackageUsageAnalyticsRequest();
        usageTracker
            .BuildReports(Arg.Any<DateTimeOffset>())
            .Returns([(AnalyticsCategory.PackageUsage, usageReport)]);
        api.PostInstallData(usageReport, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("flaky"));

        await CreateHelper().SendPeriodicReportsIfDueAsync();

        Assert.IsNotNull(settings.Analytics.LaunchDataLastSentAt);
        usageTracker.DidNotReceive().ResetCategory(Arg.Any<AnalyticsCategory>());
    }
}
