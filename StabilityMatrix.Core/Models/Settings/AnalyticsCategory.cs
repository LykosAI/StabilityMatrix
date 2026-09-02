namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// A kind of anonymous usage data the user can opt into individually. Each category maps to
/// one toggle in Settings and to one document type on the wire.
/// </summary>
public enum AnalyticsCategory
{
    /// <summary>GPU model and memory, system memory, CPU architecture, NVIDIA driver version.</summary>
    Hardware,

    /// <summary>Daily totals of package launches and run time, per package.</summary>
    PackageUsage,

    /// <summary>Daily totals of Inference generations, per tab type and model family.</summary>
    InferenceUsage,

    /// <summary>Daily totals of page visits within the app.</summary>
    FeatureUsage,

    /// <summary>Package install results and first-time setup choices.</summary>
    PackageInstalls,
}
