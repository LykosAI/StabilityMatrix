using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

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
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService,
    IRocmPackageHelper? rocmPackageHelper = null
)
    : BaseGitPackage(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    private static readonly RocmPackageProfile WindowsRocmProfile = new()
    {
        RequiresRocmSdk = true,
        UpgradePackages = true,
        PostInstallPipArgs = ["hf-xet", "setuptools", "numpy==1.26.4"],
    };

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

    public override bool IsCompatible =>
        HardwareHelper.HasNvidiaGpu()
        || (Compat.IsWindows ? HasWindowsRocmSupport() : HardwareHelper.HasAmdGpu());

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Img2Vid] = ["outputs"] };

    // Wan2GP currently uses Python 3.11 for ROCm and 3.10 for CUDA.
    public override PyVersion RecommendedPythonVersion =>
        IsAmdRocm ? Python.PyInstallationManager.Python_3_11_13 : Python.PyInstallationManager.Python_3_10_17;

    public override string Disclaimer =>
        IsAmdRocm && Compat.IsWindows
            ? "Windows AMD ROCm support is experimental. Please report any issues to Stability Matrix first so it can be determined whether the issue is package-specific.\nBecause this setup may not be officially supported by package developers, only contact upstream support for issues clearly caused by the package itself."
            : string.Empty;

    /// <summary>
    /// Helper property to check if we're using AMD ROCm
    /// </summary>
    private bool IsAmdRocm => GetRecommendedTorchVersion() == TorchIndex.Rocm;

    private bool HasWindowsRocmSupport()
    {
        if (!Compat.IsWindows)
            return false;

        if (rocmPackageHelper is null)
            return HardwareHelper.HasWindowsRocmSupportedGpu();

        return rocmPackageHelper.GetCompatibility(WindowsRocmProfile).IsCompatible;
    }

    /// <summary>
    /// Python wrapper script that patches logging to also print to stdout/stderr, so
    /// StabilityMatrix can capture the output. Wan2GP logs through Gradio UI notifications
    /// (gr.Info/Warning/Error) and callback-driven UI updates that never reach the console.
    /// This script:
    /// 1. Configures Python's logging module to output to stderr (captures library logging)
    /// 2. Prevents transformers from suppressing its own logging (wgp.py calls set_verbosity_error)
    /// 3. Monkey-patches gr.Info/Warning/Error to also print to stdout/stderr
    /// 4. Runs the target script (wgp.py) via runpy
    /// </summary>
    private const string GradioLogPatchScript = """
        # StabilityMatrix: Patch logging to print to console for capture.
        import sys
        import logging

        def _apply_logging_patch():
            # Configure Python's root logger to output to stderr at INFO level.
            # Many libraries (torch, diffusers, transformers, etc.) use the logging
            # module but output may be suppressed without a handler configured.
            root = logging.getLogger()
            if not any(isinstance(h, logging.StreamHandler) for h in root.handlers):
                handler = logging.StreamHandler(sys.stderr)
                handler.setFormatter(logging.Formatter("[%(name)s] %(levelname)s: %(message)s"))
                root.addHandler(handler)
            if root.level > logging.INFO:
                root.setLevel(logging.INFO)

            # Prevent transformers from suppressing its own logging.
            # wgp.py calls transformers.utils.logging.set_verbosity_error() which
            # silences all non-error messages. We neutralize those calls so model
            # loading and download messages remain visible.
            try:
                import transformers.utils.logging as tf_logging
                tf_logging.set_verbosity_error = lambda: None
                tf_logging.set_verbosity_warning = lambda: None
                tf_logging.set_verbosity(logging.INFO)
            except Exception as e:
                print(f"[StabilityMatrix] Failed to patch transformers logging: {e}", file=sys.stderr, flush=True)

            # Monkey-patch Gradio's UI notification functions to also print to console.
            # These only fire for validation/error messages, not generation progress.
            try:
                import gradio as gr
                _orig_info = getattr(gr, 'Info', None)
                _orig_warning = getattr(gr, 'Warning', None)
                _orig_error = getattr(gr, 'Error', None)
                if _orig_info is not None:
                    def patched_info(message, *args, **kwargs):
                        print(f"[Gradio] {message}", flush=True)
                        return _orig_info(message, *args, **kwargs)
                    gr.Info = patched_info
                if _orig_warning is not None:
                    def patched_warning(message, *args, **kwargs):
                        print(f"[Gradio] WARNING: {message}", flush=True)
                        return _orig_warning(message, *args, **kwargs)
                    gr.Warning = patched_warning
                if _orig_error is not None:
                    def patched_error(message, *args, **kwargs):
                        print(f"[Gradio] ERROR: {message}", file=sys.stderr, flush=True)
                        return _orig_error(message, *args, **kwargs)
                    gr.Error = patched_error
            except Exception as e:
                print(f"[StabilityMatrix] Failed to patch Gradio logging: {e}", file=sys.stderr, flush=True)

        if __name__ == "__main__":
            _apply_logging_patch()
            target_script = sys.argv[1]
            sys.argv = sys.argv[1:]
            import runpy
            runpy.run_path(target_script, run_name="__main__")
        """;

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--server-name"],
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--server-port"],
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
                Name = "Listen",
                Type = LaunchOptionType.Bool,
                Description = "Make server accessible on network",
                Options = ["--listen"],
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
                    WindowsRocmSupport.IsSupportedGpu(SettingsManager.Settings.PreferredGpu)
                    || HasWindowsRocmSupport()
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
                    installLocation,
                    installedPackage,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await InstallNvidiaAsync(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(1, "Install complete", isIndeterminate: false));
    }

    private async Task InstallNvidiaAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress,
        Action<ProcessOutput>? onConsoleOutput
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

        if (!isNewerNvidia)
            return;

        // Install triton n stuff for newer NVIDIA GPUs
        if (Compat.IsWindows)
        {
            progress?.Report(new ProgressReport(-1f, "Installing triton-windows...", isIndeterminate: true));
            await venvRunner
                .PipInstall("triton-windows==3.3.1.post19", onConsoleOutput)
                .ConfigureAwait(false);

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
                    "https://huggingface.co/cocktailpeanut/wheels/resolve/main/flash_attn-2.8.3%2Bcu128torch2.7-cp310-cp310-linux_x86_64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            await venvRunner.PipInstall("numpy==2.1.2", onConsoleOutput).ConfigureAwait(false);
        }
    }

    private async Task InstallAmdRocmAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        if (Compat.IsWindows)
        {
            if (rocmPackageHelper is null)
            {
                throw new InvalidOperationException(
                    "Windows ROCm installation for Wan2GP requires the shared ROCm helper."
                );
            }

            await rocmPackageHelper
                .InstallWindowsNativePackageAsync(
                    venvRunner,
                    installLocation,
                    installedPackage,
                    WindowsRocmProfile,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return;
        }

        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));
        await venvRunner.PipInstall("-r requirements.txt", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing PyTorch ROCm...", isIndeterminate: true));
        var torchArgs = new PipInstallArgs()
            .WithTorch()
            .WithTorchVision()
            .WithTorchAudio()
            .WithTorchExtraIndex("rocm7.2")
            .AddArg("--force-reinstall")
            .AddArg("--no-deps");

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        // Install additional packages
        await venvRunner.PipInstall("hf-xet setuptools numpy==1.26.4", onConsoleOutput).ConfigureAwait(false);
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

        if (Compat.IsWindows && rocmPackageHelper is not null && HasWindowsRocmSupport())
        {
            var rocmEnvironment = rocmPackageHelper.BuildLaunchEnvironment(
                installLocation,
                installedPackage,
                WindowsRocmProfile
            );
            VenvRunner.UpdateEnvironmentVariables(env => env.SetItems(rocmEnvironment));
        }

        // Fix for distutils compatibility issue with Python 3.10 and setuptools
        VenvRunner.UpdateEnvironmentVariables(env => env.SetItem("SETUPTOOLS_USE_DISTUTILS", "stdlib"));

        // Write the Gradio logging patch wrapper script so gr.Info/Warning/Error
        // messages are also printed to stdout/stderr for console capture
        var patchScriptPath = Path.Combine(installLocation, "_sm_gradio_log_patch.py");
        await File.WriteAllTextAsync(patchScriptPath, GradioLogPatchScript, cancellationToken)
            .ConfigureAwait(false);

        var targetScript = Path.Combine(installLocation, options.Command ?? LaunchCommand);

        // Notify user that the package is starting (loading can take a while)
        onConsoleOutput?.Invoke(
            new ProcessOutput { Text = "Launching Wan2GP, please wait while the UI initializes...\n" }
        );

        // Launch via the patch wrapper, which monkey-patches Gradio then runs wgp.py
        VenvRunner.RunDetached(
            [patchScriptPath, targetScript, .. options.Arguments],
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
