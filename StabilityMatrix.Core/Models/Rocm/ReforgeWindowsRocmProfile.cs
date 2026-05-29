using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for reForge.
/// </summary>
public static class ReforgeWindowsRocmProfile
{
    // reForge currently pins accelerate 0.21.0, but 0.22.0 avoids the early distributed.torch import that breaks on Windows ROCm torch builds
    // caused by the marigold depth controlnet preprocessor
    public const string WindowsRocmAccelerateVersion = "0.22.0";
    private static readonly string[] DisabledCudaLaunchOptionNames = ["CUDA Malloc", "CUDA Stream"];

    public static RocmPackageProfile Profile { get; } = CreateProfile([]);

    public static RocmPackageProfile CreateProfile(IEnumerable<string> requirementsFilePaths)
    {
        return new RocmPackageProfile
        {
            InstallConfig = new PipInstallConfig
            {
                PrePipInstallArgs = ["joblib"],
                RequirementsFilePaths = [.. requirementsFilePaths],
                ExtraPipArgs =
                [
                    "https://github.com/openai/CLIP/archive/d50d76daa670286dd6cacf3bcd80b5e4823fc8e1.zip",
                ],
                PostInstallPipArgs =
                [
                    "numpy==1.26.4",
                    "setuptools==69.5.1",
                    $"accelerate=={WindowsRocmAccelerateVersion}",
                ],
                PostTorchInstallPipArgs =
                [
                    "--index-url",
                    "https://pypi.org/simple",
                    "--force-reinstall",
                    "setuptools==69.5.1",
                ],
                UpgradePackages = true,
            },
        };
    }

    public static RocmCompatibilityResult GetCompatibility(IRocmPackageHelper? rocmPackageHelper)
    {
        if (rocmPackageHelper is null)
        {
            return new RocmCompatibilityResult { IsCompatible = false };
        }

        return rocmPackageHelper.GetCompatibility();
    }

    public static bool HasSupport(IRocmPackageHelper? rocmPackageHelper)
    {
        return GetCompatibility(rocmPackageHelper).IsCompatible;
    }

    public static bool ShouldUseWindowsNativeInstall(
        IRocmPackageHelper? rocmPackageHelper,
        TorchIndex selectedTorchIndex
    )
    {
        return Compat.IsWindows && selectedTorchIndex == TorchIndex.Rocm && HasSupport(rocmPackageHelper);
    }

    public static void ApplyWindowsRocmLaunchDefaults(
        List<LaunchOptionDefinition> launchOptions,
        IRocmPackageHelper? rocmPackageHelper
    )
    {
        if (!(Compat.IsWindows && HasSupport(rocmPackageHelper)))
        {
            return;
        }

        foreach (var optionName in DisabledCudaLaunchOptionNames)
        {
            var optionIndex = launchOptions.FindIndex(x => x.Name == optionName);
            if (optionIndex < 0)
            {
                continue;
            }

            launchOptions[optionIndex] = launchOptions[optionIndex] with { InitialValue = null };
        }
    }

    public static string? GetPreferredCrossAttentionArgument(IRocmPackageHelper? rocmPackageHelper)
    {
        var compatibility = GetCompatibility(rocmPackageHelper);
        if (!compatibility.IsCompatible)
        {
            return null;
        }

        return WindowsRocmSupport.PreferLegacyAttentionFallback(compatibility.ResolvedGfxArch)
            ? "--attention-quad"
            : "--attention-pytorch";
    }

    public static IReadOnlyDictionary<string, string> BuildLaunchEnvironment(
        IRocmPackageHelper? rocmPackageHelper
    )
    {
        return rocmPackageHelper?.BuildLaunchEnvironment(Profile) ?? new Dictionary<string, string>();
    }

    public static IReadOnlyList<string> GetLaunchNoticeLines(
        IRocmPackageHelper? rocmPackageHelper,
        TorchIndex selectedTorchIndex
    )
    {
        return ShouldUseWindowsNativeInstall(rocmPackageHelper, selectedTorchIndex)
            ? rocmPackageHelper?.GetWindowsLaunchNoticeLines() ?? []
            : [];
    }
}
