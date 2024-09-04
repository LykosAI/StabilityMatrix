namespace StabilityMatrix.Core.Helper.Analytics;

public interface IAnalyticsHelper
{
    Task TrackInstallAsync(string packageName, string packageVersion, bool isSuccess, string? reason = null);

    Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped,
        string platform
    );
}
