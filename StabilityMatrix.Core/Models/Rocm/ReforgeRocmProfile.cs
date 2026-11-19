using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared ROCm profile for reForge.
/// </summary>
public class ReforgeRocmProfile : RocmPackageProfile
{
    // reForge currently pins accelerate 0.21.0, but 0.22.0 avoids the early distributed.torch import that breaks on Windows ROCm torch builds
    // caused by the marigold depth controlnet preprocessor
    public const string AccelerateVersion = "0.22.0";
    private static readonly string[] DisabledCudaLaunchOptionNames = ["CUDA Malloc", "CUDA Stream"];

    public static ReforgeRocmProfile Default { get; } = new ReforgeRocmProfile();

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
                    $"accelerate=={AccelerateVersion}",
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

    public override void ApplyRocmLaunchDefaults(
        List<LaunchOptionDefinition> launchOptions,
        IRocmPackageHelper rocmPackageHelper
    )
    {
        if (!(Compat.IsWindows && rocmPackageHelper.GetCompatibility().IsCompatible))
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

    public override string? GetPreferredCrossAttentionArgument(IRocmPackageHelper rocmPackageHelper)
    {
        var compatibility = rocmPackageHelper.GetCompatibility();
        if (!compatibility.IsCompatible)
        {
            return null;
        }

        return RocmSupport.PreferLegacyAttentionFallback(compatibility.ResolvedGfxArch)
            ? "--attention-quad"
            : "--attention-pytorch";
    }
}
