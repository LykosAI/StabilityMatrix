using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

/// <summary>
/// Package for Wan2GP - Super Optimized Gradio UI for AI video creation.
/// Supports Wan 2.1/2.2, Qwen, Hunyuan Video, LTX Video and Flux.
/// https://github.com/deepbeepmeep/Wan2GP
/// </summary>
/// <remarks>
/// <b>Model Sharing:</b> This package does not support Stability Matrix shared folder configuration.
/// Wan2GP manages model paths through its own wgp_config.json file, which is created and managed
/// by the Gradio UI on first launch. Users should configure model paths via the Settings tab
/// in the Wan2GP UI.
/// </remarks>
[RegisterSingleton<BasePackage, Wan2GP>(Duplicate = DuplicateStrategy.Append)]
public class Wan2GP(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "Wan2GP";
    public override string DisplayName { get; set; } = "Wan2GP";
    public override string Author => "deepbeepmeep";

    public override string Blurb =>
        "Super Optimized Gradio UI for AI video creation for GPU poor machines (6GB+ VRAM). "
        + "Supports Wan 2.1/2.2, Qwen, Hunyuan Video, LTX Video and Flux.";

    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/deepbeepmeep/Wan2GP/blob/main/LICENSE.txt";
    public override string LaunchCommand => "wgp.py";

    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/wan2gp/wan2gp.webp");

    public override string OutputFolderName => "outputs";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods => [SharedFolderMethod.None];

    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda, TorchIndex.Rocm];

    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu() || HardwareHelper.HasAmdGpu();

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Img2Vid] = ["outputs"] };

    // AMD ROCm requires Python 3.11, NVIDIA uses 3.10
    public override PyVersion RecommendedPythonVersion =>
        IsAmdRocm ? Python.PyInstallationManager.Python_3_11_13 : Python.PyInstallationManager.Python_3_10_17;

    public override string Disclaimer =>
        IsAmdRocm && Compat.IsWindows
            ? "AMD GPU support on Windows is experimental. Supported GPUs: 7900(XT), 7800(XT), 7600(XT), Phoenix, 9070(XT) and Strix Halo."
            : string.Empty;

    /// <summary>
    /// Helper property to check if we're using AMD ROCm
    /// </summary>
    private bool IsAmdRocm => GetRecommendedTorchVersion() == TorchIndex.Rocm;

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--server"],
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"],
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = ["--share"],
            },
            new()
            {
                Name = "Multiple Images",
                Type = LaunchOptionType.Bool,
                Description = "Enable multiple images mode",
                InitialValue = true,
                Options = ["--multiple-images"],
            },
            new()
            {
                Name = "Compile",
                Type = LaunchOptionType.Bool,
                Description = "Enable model compilation for faster inference (may not work on all systems)",
                Options = ["--compile"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override TorchIndex GetRecommendedTorchVersion()
    {
        // Check for AMD ROCm support (Windows or Linux)
        var preferRocm =
            (
                Compat.IsWindows
                && (
                    SettingsManager.Settings.PreferredGpu?.IsWindowsRocmSupportedGpu()
                    ?? HardwareHelper.HasWindowsRocmSupportedGpu()
                )
            )
            || (
                Compat.IsLinux
                && (SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.PreferRocm())
            );

        if (preferRocm)
        {
            return TorchIndex.Rocm;
        }

        // NVIDIA CUDA
        if (SettingsManager.Settings.PreferredGpu?.IsNvidia ?? HardwareHelper.HasNvidiaGpu())
        {
            return TorchIndex.Cuda;
        }

        return base.GetRecommendedTorchVersion();
    }

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        if (torchIndex == TorchIndex.Rocm)
        {
            await InstallAmdRocmAsync(
                    venvRunner,
                    installedPackage,
                    options,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await InstallNvidiaAsync(
                    venvRunner,
                    installedPackage,
                    options,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(1, "Install complete", isIndeterminate: false));
    }

    private async Task InstallNvidiaAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        var isLegacyNvidia =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();
        var isNewerNvidia =
            SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu()
            ?? HardwareHelper.HasAmpereOrNewerGpu();

        // Platform-specific versions from pinokio torch.js
        // Windows: torch 2.7.1, Linux: torch 2.7.0 (to match prebuilt attention wheel requirements)
        var torchVersion = Compat.IsWindows ? "2.7.1" : "2.7.0";
        var torchvisionVersion = Compat.IsWindows ? "0.22.1" : "0.22.0";
        var torchaudioVersion = Compat.IsWindows ? "2.7.1" : "2.7.0";
        var cudaIndex = isLegacyNvidia ? "cu126" : "cu128";

        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        // Install requirements directly using -r flag (handles @ URL syntax properly)
        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));
        await venvRunner.PipInstall("-r requirements.txt", onConsoleOutput).ConfigureAwait(false);

        // Install torch with specific versions and CUDA index (force reinstall to ensure correct version)
        progress?.Report(new ProgressReport(-1f, "Installing PyTorch...", isIndeterminate: true));
        var torchArgs = new PipInstallArgs()
            .WithTorch($"=={torchVersion}")
            .WithTorchVision($"=={torchvisionVersion}")
            .WithTorchAudio($"=={torchaudioVersion}")
            .WithXFormers("==0.0.30")
            .WithTorchExtraIndex(cudaIndex)
            .AddArg("--force-reinstall")
            .AddArg("--no-deps");

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        // Install hf-xet and pin setuptools to avoid distutils compatibility issues with Python 3.10
        await venvRunner.PipInstall("hf-xet \"setuptools<70.0.0\"", onConsoleOutput).ConfigureAwait(false);

        // Install triton-windows for newer NVIDIA GPUs on Windows
        if (Compat.IsWindows && isNewerNvidia)
        {
            progress?.Report(new ProgressReport(-1f, "Installing triton-windows...", isIndeterminate: true));
            await venvRunner
                .PipInstall("triton-windows==3.3.1.post19", onConsoleOutput)
                .ConfigureAwait(false);
        }

        // Install SageAttention and Flash Attention
        if (Compat.IsWindows)
        {
            progress?.Report(new ProgressReport(-1f, "Installing SageAttention...", isIndeterminate: true));
            await venvRunner
                .PipInstall(
                    "https://github.com/woct0rdho/SageAttention/releases/download/v2.2.0-windows/sageattention-2.2.0+cu128torch2.7.1-cp310-cp310-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing Flash Attention...", isIndeterminate: true));
            await venvRunner
                .PipInstall(
                    "https://huggingface.co/lldacing/flash-attention-windows-wheel/resolve/main/flash_attn-2.7.4.post1%2Bcu128torch2.7.0cxx11abiFALSE-cp310-cp310-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);
        }
        else if (Compat.IsLinux)
        {
            progress?.Report(new ProgressReport(-1f, "Installing SageAttention...", isIndeterminate: true));
            await venvRunner
                .PipInstall(
                    "https://huggingface.co/MonsterMMORPG/SECourses_Premium_Flash_Attention/resolve/main/sageattention-2.1.1-cp310-cp310-linux_x86_64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing Flash Attention...", isIndeterminate: true));
            await venvRunner
                .PipInstall(
                    "https://github.com/kingbri1/flash-attention/releases/download/v2.7.4.post1/flash_attn-2.7.4.post1+cu128torch2.7.0cxx11abiFALSE-cp310-cp310-linux_x86_64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            await venvRunner.PipInstall("numpy==2.1.2", onConsoleOutput).ConfigureAwait(false);
        }
    }

    private async Task InstallAmdRocmAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        if (Compat.IsWindows)
        {
            // Windows AMD ROCm - special TheRock wheels
            progress?.Report(
                new ProgressReport(-1f, "Installing PyTorch ROCm wheels...", isIndeterminate: true)
            );

            // Set environment variable for wheel filename check bypass
            venvRunner.UpdateEnvironmentVariables(env => env.SetItem("UV_SKIP_WHEEL_FILENAME_CHECK", "1"));

            // Install PyTorch ROCm wheels from TheRock releases (Python 3.11)
            await venvRunner
                .PipInstall(
                    "https://github.com/scottt/rocm-TheRock/releases/download/v6.5.0rc-pytorch-gfx110x/torch-2.7.0a0+rocm_git3f903c3-cp311-cp311-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            await venvRunner
                .PipInstall(
                    "https://github.com/scottt/rocm-TheRock/releases/download/v6.5.0rc-pytorch-gfx110x/torchaudio-2.7.0a0+52638ef-cp311-cp311-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            await venvRunner
                .PipInstall(
                    "https://github.com/scottt/rocm-TheRock/releases/download/v6.5.0rc-pytorch-gfx110x/torchvision-0.22.0+9eb57cd-cp311-cp311-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            // Install requirements directly using -r flag (handles @ URL syntax properly)
            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));
            await venvRunner.PipInstall("-r requirements.txt", onConsoleOutput).ConfigureAwait(false);

            // Install additional packages
            await venvRunner
                .PipInstall("hf-xet setuptools numpy==1.26.4", onConsoleOutput)
                .ConfigureAwait(false);
        }
        else
        {
            // Linux AMD ROCm - standard PyTorch ROCm
            // Install requirements directly using -r flag (handles @ URL syntax properly)
            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));
            await venvRunner.PipInstall("-r requirements.txt", onConsoleOutput).ConfigureAwait(false);

            // Install torch with ROCm index (force reinstall to ensure correct version)
            progress?.Report(new ProgressReport(-1f, "Installing PyTorch ROCm...", isIndeterminate: true));
            var torchArgs = new PipInstallArgs()
                .WithTorch("==2.7.0")
                .WithTorchVision("==0.22.0")
                .WithTorchAudio("==2.7.0")
                .WithTorchExtraIndex("rocm6.3")
                .AddArg("--force-reinstall")
                .AddArg("--no-deps");

            await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

            // Install additional packages
            await venvRunner
                .PipInstall("hf-xet setuptools numpy==1.26.4", onConsoleOutput)
                .ConfigureAwait(false);
        }
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        // Fix for distutils compatibility issue with Python 3.10 and setuptools
        VenvRunner.UpdateEnvironmentVariables(env => env.SetItem("SETUPTOOLS_USE_DISTUTILS", "stdlib"));

        // Notify user that the package is starting (loading can take a while)
        onConsoleOutput?.Invoke(
            new ProcessOutput { Text = "Launching Wan2GP, please wait while the UI initializes...\n" }
        );

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. options.Arguments],
            HandleConsoleOutput,
            OnExit
        );

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (match.Success)
            {
                WebUrl = match.Value;
                OnStartupComplete(WebUrl);
            }
        }
    }
}
