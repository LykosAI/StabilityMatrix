using Microsoft.Extensions.Logging;
using NSubstitute;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnalyticsUsageTrackerTests
{
    private string tempDir = null!;
    private Settings settings = null!;
    private ISettingsManager settingsManager = null!;

    [TestInitialize]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "sm-analytics-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        settings = new Settings
        {
            Analytics = new AnalyticsSettings
            {
                IsUsageDataEnabled = true,
                IsPackageUsageDataEnabled = true,
                IsInferenceUsageDataEnabled = true,
                IsFeatureUsageDataEnabled = true,
            },
        };

        settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(settings);
        settingsManager.IsLibraryDirSet.Returns(true);
        settingsManager.LibraryDir.Returns(new DirectoryPath(tempDir));
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(tempDir, true);
    }

    private AnalyticsUsageTracker CreateTracker() =>
        new(Substitute.For<ILogger<AnalyticsUsageTracker>>(), settingsManager);

    [TestMethod]
    public void Record_AggregatesIntoDailyReports()
    {
        using var tracker = CreateTracker();

        tracker.RecordPackageLaunch("ComfyUI");
        tracker.RecordPackageLaunch("ComfyUI");
        tracker.RecordPackageLaunch("Forge");
        tracker.RecordPackageRun("ComfyUI", TimeSpan.FromMinutes(90));
        tracker.RecordPackageRun("ComfyUI", TimeSpan.FromSeconds(100));
        tracker.RecordInferenceGeneration("TextToImage", "Flux");
        tracker.RecordInferenceGeneration("TextToImage", null);
        tracker.RecordInferenceGeneration("Upscale", null);
        tracker.RecordPageVisit("Inference");

        var now = DateTimeOffset.UtcNow;
        var reports = tracker.BuildReports(now).ToDictionary(r => r.Category, r => r.Request);

        Assert.AreEqual(3, reports.Count);

        var packages = (PackageUsageAnalyticsRequest)reports[AnalyticsCategory.PackageUsage];
        Assert.AreEqual(2, packages.Launches["ComfyUI"]);
        Assert.AreEqual(1, packages.Launches["Forge"]);
        Assert.AreEqual(92, packages.RunMinutes["ComfyUI"]);
        Assert.AreEqual(now, packages.PeriodEnd);
        Assert.IsTrue(packages.PeriodStart <= now);

        var inference = (InferenceUsageAnalyticsRequest)reports[AnalyticsCategory.InferenceUsage];
        Assert.AreEqual(2, inference.Generations["TextToImage"]);
        Assert.AreEqual(1, inference.Generations["Upscale"]);
        Assert.AreEqual(1, inference.ModelFamilies["Flux"]);
        Assert.AreEqual(1, inference.ModelFamilies.Count);

        var features = (FeatureUsageAnalyticsRequest)reports[AnalyticsCategory.FeatureUsage];
        Assert.AreEqual(1, features.PageVisits["Inference"]);
    }

    [TestMethod]
    public void Record_IsIgnoredWhenCategoryDisabled()
    {
        settings.Analytics.IsInferenceUsageDataEnabled = false;

        using var tracker = CreateTracker();

        tracker.RecordInferenceGeneration("TextToImage", "Flux");
        tracker.RecordPageVisit("Inference");

        var reports = tracker.BuildReports(DateTimeOffset.UtcNow);

        Assert.AreEqual(1, reports.Count);
        Assert.AreEqual(AnalyticsCategory.FeatureUsage, reports[0].Category);
    }

    [TestMethod]
    public void Record_IsIgnoredWhenMasterSwitchOff()
    {
        settings.Analytics.IsUsageDataEnabled = false;

        using var tracker = CreateTracker();

        tracker.RecordPackageLaunch("ComfyUI");
        tracker.RecordInferenceGeneration("TextToImage", null);
        tracker.RecordPageVisit("Inference");

        Assert.AreEqual(0, tracker.BuildReports(DateTimeOffset.UtcNow).Count);
    }

    [TestMethod]
    public void BuildReports_OmitsCategoriesDisabledAfterRecording()
    {
        using var tracker = CreateTracker();

        tracker.RecordPackageLaunch("ComfyUI");
        settings.Analytics.IsPackageUsageDataEnabled = false;

        Assert.AreEqual(0, tracker.BuildReports(DateTimeOffset.UtcNow).Count);
    }

    [TestMethod]
    public void ResetCategory_ClearsOnlyThatCategory()
    {
        using var tracker = CreateTracker();

        tracker.RecordPackageLaunch("ComfyUI");
        tracker.RecordPageVisit("Inference");

        tracker.ResetCategory(AnalyticsCategory.PackageUsage);

        var reports = tracker.BuildReports(DateTimeOffset.UtcNow);

        Assert.AreEqual(1, reports.Count);
        Assert.AreEqual(AnalyticsCategory.FeatureUsage, reports[0].Category);
    }

    [TestMethod]
    public void Counters_SurviveRestart()
    {
        using (var tracker = CreateTracker())
        {
            tracker.RecordPackageLaunch("ComfyUI");
            tracker.RecordPackageRun("ComfyUI", TimeSpan.FromMinutes(10));
            tracker.RecordInferenceGeneration("TextToImage", "Flux");
        }

        Assert.IsTrue(File.Exists(Path.Combine(tempDir, "analytics-usage.json")));

        using var restarted = CreateTracker();
        restarted.RecordPackageLaunch("ComfyUI");

        var reports = restarted
            .BuildReports(DateTimeOffset.UtcNow)
            .ToDictionary(r => r.Category, r => r.Request);

        var packages = (PackageUsageAnalyticsRequest)reports[AnalyticsCategory.PackageUsage];
        Assert.AreEqual(2, packages.Launches["ComfyUI"]);
        Assert.AreEqual(10, packages.RunMinutes["ComfyUI"]);

        var inference = (InferenceUsageAnalyticsRequest)reports[AnalyticsCategory.InferenceUsage];
        Assert.AreEqual(1, inference.ModelFamilies["Flux"]);
    }
}
