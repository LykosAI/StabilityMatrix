namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// Paths of the documentation pages that in-app help affordances link to, relative to the
/// docs root. Contextual help buttons reference these instead of spelling out a path, so a
/// page that moves is renamed in one place rather than hunted down across the UI — and a
/// typo fails to compile instead of silently resolving to a missing page at runtime.
/// </summary>
public static class DocumentationPages
{
    public const string ComfyUiIntegration = "advanced/comfyui-integration.md";
    public const string CommonIssues = "troubleshooting/common-issues.md";
    public const string DataDirectory = "getting-started/data-directory.md";
    public const string EnvironmentVariables = "advanced/environment-variables.md";
    public const string FirstLaunch = "getting-started/first-launch.md";
    public const string HardwareSupport = "advanced/hardware-support.md";
    public const string InferenceOverview = "inference/overview.md";
    public const string Installation = "getting-started/installation.md";
    public const string InstallingPackages = "package-manager/installing-packages.md";
    public const string Settings = "settings/settings.md";
    public const string SupportedPackages = "package-manager/supported-packages.md";
    public const string Terminology = "tips/terminology.md";
}
