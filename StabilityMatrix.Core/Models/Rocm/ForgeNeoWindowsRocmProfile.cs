using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Windows ROCm profile for Forge Neo.
/// Forge Neo has no native AMD support, so the helper owns the ROCm torch install and the launch
/// environment, while Forge Neo's own launch.py still installs the rest of its dependencies.
/// </summary>
public class ForgeNeoWindowsRocmProfile : RocmPackageProfile
{
    private const string DisableSmartMemoryOptionName = "Disable Smart Memory";
    private const string UsePytorchCrossAttentionOptionName = "Use PyTorch Cross Attention";

    // Recommended launch defaults from the "AMD Forge Neo with ROCm" setup guide.
    private static readonly string[] EnableLaunchOptionNames = ["Pin Shared Memory", "CUDA Stream"];
    private static readonly string[] DisableLaunchOptionNames = ["CUDA Malloc"];

    public ForgeNeoWindowsRocmProfile()
    {
        InstallConfig = new PipInstallConfig
        {
            // Forge Neo installs its own requirements via launch.py; the helper only needs to add the
            // ROCm-oriented accelerators that upstream would otherwise pull as CUDA-only builds.
            PostTorchInstallPipArgs = ["bitsandbytes", "triton-windows"],
            UpgradePackages = true,
        };

        ExtraEnvironmentFactory = BuildEnvironment;
    }

    public static ForgeNeoWindowsRocmProfile Default { get; } = new ForgeNeoWindowsRocmProfile();

    private static IReadOnlyDictionary<string, string> BuildEnvironment(RocmRuntimeContext runtimeContext)
    {
        var indexUrl = WindowsRocmSupport.GetMultiArchPythonPackageIndexUrl(runtimeContext.RuntimeGfxArch);
        var deviceExtra = WindowsRocmSupport.TryGetMultiArchDeviceExtra(runtimeContext.RuntimeGfxArch);

        if (string.IsNullOrWhiteSpace(deviceExtra))
        {
            return new Dictionary<string, string>();
        }

        // Persisted so that if Forge Neo re-runs its own torch install step it targets AMD's index
        // instead of pulling the NVIDIA CUDA build from PyTorch.
        var torchCommand =
            $"pip install --index-url {indexUrl} "
            + $"\"torch[{deviceExtra}]\" \"torchvision[{deviceExtra}]\" torchaudio \"rocm[devel]\"";

        return new Dictionary<string, string>
        {
            ["TORCH_INDEX_URL"] = indexUrl,
            ["TORCH_COMMAND"] = torchCommand,
        };
    }

    public override void ApplyWindowsRocmLaunchDefaults(
        List<LaunchOptionDefinition> launchOptions,
        IRocmPackageHelper rocmPackageHelper
    )
    {
        if (!(Compat.IsWindows && rocmPackageHelper.GetCompatibility().IsCompatible))
        {
            return;
        }

        for (var i = 0; i < launchOptions.Count; i++)
        {
            var option = launchOptions[i];

            if (EnableLaunchOptionNames.Contains(option.Name))
            {
                launchOptions[i] = option with { InitialValue = true };
            }
            else if (DisableLaunchOptionNames.Contains(option.Name))
            {
                launchOptions[i] = option with { InitialValue = null };
            }
        }

        InsertBoolOptionIfMissing(
            launchOptions,
            DisableSmartMemoryOptionName,
            "--disable-smart-memory",
            "Aggressively offload to RAM instead of keeping models in VRAM when possible"
        );
        InsertBoolOptionIfMissing(
            launchOptions,
            UsePytorchCrossAttentionOptionName,
            "--use-pytorch-cross-attention",
            "Use the PyTorch cross attention (override sageattention/flash_attn/xformers)"
        );
    }

    private static void InsertBoolOptionIfMissing(
        List<LaunchOptionDefinition> launchOptions,
        string name,
        string option,
        string description
    )
    {
        if (launchOptions.Any(x => x.Name == name))
        {
            return;
        }

        var definition = new LaunchOptionDefinition
        {
            Name = name,
            Type = LaunchOptionType.Bool,
            Description = description,
            Options = [option],
            InitialValue = true,
        };

        var extrasIndex = launchOptions.FindIndex(x => x.Name == LaunchOptionDefinition.Extras.Name);
        launchOptions.Insert(extrasIndex < 0 ? launchOptions.Count : extrasIndex, definition);
    }
}
