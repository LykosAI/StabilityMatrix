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
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Tracking_RespectsUsageDataToggle(bool enabled)
    {
        var settingsManager = Substitute.For<ISettingsManager>();
        var settings = new Settings();
        settings.Analytics.IsUsageDataEnabled = enabled;
        settingsManager.Settings.Returns(settings);
        var api = Substitute.For<ILykosAnalyticsApi>();
        var sent = new List<AnalyticsRequest>();
        api.PostInstallData(Arg.Do<AnalyticsRequest>(sent.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var helper = new AnalyticsHelper(Substitute.For<ILogger<AnalyticsHelper>>(), api, settingsManager);

        await helper.TrackAsync(new LaunchAnalyticsRequest());
        await helper.TrackPackageInstallAsync("ComfyUI", "1.0", true);
        await helper.TrackFirstTimeInstallAsync("ComfyUI", ["recommended-model"], false);

        CollectionAssert.AreEqual(
            enabled
                ? new[]
                {
                    typeof(LaunchAnalyticsRequest),
                    typeof(PackageInstallAnalyticsRequest),
                    typeof(FirstTimeInstallAnalytics),
                }
                : Array.Empty<Type>(),
            sent.Select(request => request.GetType()).ToArray()
        );
        settingsManager.DidNotReceive().Transaction(Arg.Any<Action<Settings>>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task Tracking_ApiFailureDoesNotInterruptApp()
    {
        var settingsManager = Substitute.For<ISettingsManager>();
        var settings = new Settings();
        settings.Analytics.IsUsageDataEnabled = true;
        settingsManager.Settings.Returns(settings);
        var api = Substitute.For<ILykosAnalyticsApi>();
        api.PostInstallData(Arg.Any<AnalyticsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var helper = new AnalyticsHelper(Substitute.For<ILogger<AnalyticsHelper>>(), api, settingsManager);

        await helper.TrackAsync(new LaunchAnalyticsRequest());
        await helper.TrackPackageInstallAsync("ComfyUI", "1.0", false);
        await helper.TrackFirstTimeInstallAsync(null, null, true);

        await api.Received(3).PostInstallData(Arg.Any<AnalyticsRequest>(), Arg.Any<CancellationToken>());
    }
}
