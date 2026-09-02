using System.Runtime.InteropServices;
using Semver;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnalyticsSettingsTests
{
    [TestMethod]
    public void FreshInstall_IsUnansweredNotOutdated()
    {
        var settings = new AnalyticsSettings();

        Assert.IsTrue(settings.IsConsentUnanswered);
        Assert.IsFalse(settings.IsConsentOutdated);
    }

    [TestMethod]
    public void OptedInUnderOriginalDialog_IsOutdated()
    {
        var settings = new AnalyticsSettings
        {
            LastSeenConsentVersion = SemVersion.Parse("2.16.0", SemVersionStyles.Strict),
            LastSeenConsentAccepted = true,
            IsUsageDataEnabled = true,
        };

        Assert.IsFalse(settings.IsConsentUnanswered);
        Assert.IsTrue(settings.IsConsentOutdated);
    }

    [TestMethod]
    public void DeclinedUnderOriginalDialog_IsNotAskedAgain()
    {
        var settings = new AnalyticsSettings
        {
            LastSeenConsentVersion = SemVersion.Parse("2.16.0", SemVersionStyles.Strict),
            LastSeenConsentAccepted = false,
            IsUsageDataEnabled = false,
        };

        Assert.IsFalse(settings.IsConsentUnanswered);
        Assert.IsFalse(settings.IsConsentOutdated);
    }

    [TestMethod]
    public void AnsweredCurrentRevision_IsNotOutdated()
    {
        var settings = new AnalyticsSettings
        {
            LastSeenConsentVersion = SemVersion.Parse("2.17.0", SemVersionStyles.Strict),
            LastSeenConsentAccepted = true,
            LastSeenConsentRevision = AnalyticsSettings.CurrentConsentRevision,
            IsUsageDataEnabled = true,
        };

        Assert.IsFalse(settings.IsConsentOutdated);
    }

    [TestMethod]
    public void CategoryEnabled_RequiresMasterSwitch()
    {
        var settings = new AnalyticsSettings();
        settings.SetCategoryFlag(AnalyticsCategory.Hardware, true);

        Assert.IsTrue(settings.GetCategoryFlag(AnalyticsCategory.Hardware));
        Assert.IsFalse(settings.IsCategoryEnabled(AnalyticsCategory.Hardware));

        settings.IsUsageDataEnabled = true;
        Assert.IsTrue(settings.IsCategoryEnabled(AnalyticsCategory.Hardware));
        Assert.IsFalse(settings.IsCategoryEnabled(AnalyticsCategory.FeatureUsage));
    }

    [TestMethod]
    public void HardwareRequest_RoundsToWholeGigabytes()
    {
        var gpus = new[]
        {
            new GpuInfo
            {
                Name = "NVIDIA GeForce RTX 4090",
                MemoryBytes = 24_564_000_000,
                ComputeCapability = "8.9",
            },
            new GpuInfo { Name = "AMD Radeon RX 7900 XTX", MemoryBytes = 24UL * 1024 * 1024 * 1024 },
        };
        var memory = new MemoryInfo
        {
            TotalInstalledBytes = 64UL * 1024 * 1024 * 1024,
            TotalPhysicalBytes = 68_400_000_000,
        };

        var request = HardwareAnalyticsRequest.Create(gpus, memory, new Version("591.59"), Architecture.X64);

        Assert.AreEqual("hardware", request.Type);
        Assert.AreEqual(2, request.Gpus.Count);
        Assert.AreEqual(23, request.Gpus[0].MemoryGb);
        Assert.AreEqual("8.9", request.Gpus[0].ComputeCapability);
        Assert.AreEqual(24, request.Gpus[1].MemoryGb);
        Assert.IsNull(request.Gpus[1].ComputeCapability);
        Assert.AreEqual(64, request.TotalMemoryGb);
        Assert.AreEqual("591.59", request.NvidiaDriverVersion);
        Assert.AreEqual("X64", request.ProcessArchitecture);
    }

    [TestMethod]
    public void HardwareRequest_ToleratesMissingInfo()
    {
        var request = HardwareAnalyticsRequest.Create([], null, null, Architecture.Arm64);

        Assert.AreEqual(0, request.Gpus.Count);
        Assert.IsNull(request.TotalMemoryGb);
        Assert.IsNull(request.NvidiaDriverVersion);
        Assert.AreEqual("Arm64", request.ProcessArchitecture);
    }
}
