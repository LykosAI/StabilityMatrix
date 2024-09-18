using StabilityMatrix.Core.Models.Api.Lykos.Analytics;

namespace StabilityMatrix.Core.Helper.Analytics;

public interface IAnalyticsHelper
{
    Task TrackPackageInstallAsync(string packageName, string packageVersion, bool isSuccess);

    Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped
    );

    Task TrackAsync(AnalyticsRequest data);
}
