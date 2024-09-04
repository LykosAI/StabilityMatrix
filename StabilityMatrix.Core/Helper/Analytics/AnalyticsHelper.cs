using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;

namespace StabilityMatrix.Core.Helper.Analytics;

[Singleton(typeof(IAnalyticsHelper))]
public class AnalyticsHelper(ILogger<AnalyticsHelper> logger, ILykosAnalyticsApi analyticsApi)
    : IAnalyticsHelper
{
    public async Task TrackInstallAsync(
        string packageName,
        string packageVersion,
        bool isSuccess,
        string? reason = null
    )
    {
        var data = new PackageInstallAnalyticsRequest
        {
            PackageName = packageName,
            PackageVersion = packageVersion,
            IsSuccess = isSuccess,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = reason
        };

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending install data");
        }
    }

    public async Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped,
        string platform
    )
    {
        var data = new FirstTimeInstallAnalytics
        {
            SelectedPackageName = selectedPackageName,
            SelectedRecommendedModels = selectedRecommendedModels,
            FirstTimeSetupSkipped = firstTimeSetupSkipped,
            Platform = platform,
            Timestamp = DateTimeOffset.UtcNow
        };

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending first time install data");
        }
    }
}
