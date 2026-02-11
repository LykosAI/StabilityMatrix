using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, ForgeClassic>(Duplicate = DuplicateStrategy.Append)]
public class ForgeClassic(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : SDWebForge(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string LegacyUpgradeAlert = "You are updating from an old version";
    private const string ContinuePrompt = "Press Enter to Continue";
    public override PyVersion? MinimumPythonVersion => Python.PyInstallationManager.Python_3_13_12;

    public override string Name => "forge-classic";
    public override string Author => "Haoming02";
    public override string RepositoryName => "sd-webui-forge-classic";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge - Classic";
    public override string MainBranch => "classic";

    public override string Blurb =>
        "This fork is focused exclusively on SD1 and SDXL checkpoints, having various optimizations implemented, with the main goal of being the lightest WebUI without any bloatwares.";
    public override string LicenseUrl =>
        "https://github.com/Haoming02/sd-webui-forge-classic/blob/classic/LICENSE";
    public override Uri PreviewImageUri =>
        new("https://github.com/Haoming02/sd-webui-forge-classic/raw/classic/html/ui.webp");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.ReallyRecommended;
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_13_12;
    public override PackageType PackageType => PackageType.Legacy;

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = ["output/extras-images"],
            [SharedOutputType.Saved] = ["output/images"],
            [SharedOutputType.Img2Img] = ["output/img2img-images"],
            [SharedOutputType.Text2Img] = ["output/txt2img-images"],
            [SharedOutputType.Img2ImgGrids] = ["output/img2img-grids"],
            [SharedOutputType.Text2ImgGrids] = ["output/txt2img-grids"],
            [SharedOutputType.SVD] = ["output/videos"],
        };

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--server-name"],
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
                Options = { "--share" },
            },
            new()
            {
                Name = "Xformers",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to use xformers",
                Options = { "--xformers" },
            },
            new()
            {
                Name = "Use SageAttention",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to use sage attention",
                Options = { "--sage" },
            },
            new()
            {
                Name = "Pin Shared Memory",
                Type = LaunchOptionType.Bool,
                Options = { "--pin-shared-memory" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false,
            },
            new()
            {
                Name = "CUDA Malloc",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-malloc" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false,
            },
            new()
            {
                Name = "CUDA Stream",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-stream" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false,
            },
            new()
            {
                Name = "Auto Launch",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to auto launch the webui",
                Options = { "--autolaunch" },
            },
            new()
            {
                Name = "Skip Python Version Check",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to skip python version check",
                Options = { "--skip-python-version-check" },
                InitialValue = true,
            },
            LaunchOptionDefinition.Extras,
        ];

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = ["models/Stable-diffusion/sd"],
            [SharedFolderType.ESRGAN] = ["models/ESRGAN"],
            [SharedFolderType.Lora] = ["models/Lora"],
            [SharedFolderType.LyCORIS] = ["models/LyCORIS"],
            [SharedFolderType.ApproxVAE] = ["models/VAE-approx"],
            [SharedFolderType.VAE] = ["models/VAE"],
            [SharedFolderType.DeepDanbooru] = ["models/deepbooru"],
            [SharedFolderType.Embeddings] = ["models/embeddings"],
            [SharedFolderType.Hypernetwork] = ["models/hypernetworks"],
            [SharedFolderType.ControlNet] = ["models/controlnet/ControlNet"],
            [SharedFolderType.AfterDetailer] = ["models/adetailer"],
            [SharedFolderType.T2IAdapter] = ["models/controlnet/T2IAdapter"],
            [SharedFolderType.IpAdapter] = ["models/controlnet/IpAdapter"],
            [SharedFolderType.IpAdapters15] = ["models/controlnet/DiffusersIpAdapters"],
            [SharedFolderType.IpAdaptersXl] = ["models/controlnet/DiffusersIpAdaptersXL"],
            [SharedFolderType.TextEncoders] = ["models/text_encoder"],
            [SharedFolderType.DiffusionModels] = ["models/Stable-diffusion/unet"],
        };

    public override List<ExtraPackageCommand> GetExtraCommands()
    {
        var commands = new List<ExtraPackageCommand>();

        if (Compat.IsWindows && SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() is true)
        {
            commands.Add(
                new ExtraPackageCommand
                {
                    CommandName = "Install Triton and SageAttention",
                    Command = InstallTritonAndSageAttention,
                }
            );
        }

        return commands;
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
        var requestedPythonVersion =
            options.PythonOptions.PythonVersion
            ?? (
                PyVersion.TryParse(installedPackage.PythonVersion, out var parsedVersion)
                    ? parsedVersion
                    : RecommendedPythonVersion
            );

        var shouldUpgradePython = options.IsUpdate && requestedPythonVersion < MinimumPythonVersion;
        var targetPythonVersion = shouldUpgradePython ? MinimumPythonVersion!.Value : requestedPythonVersion;

        if (shouldUpgradePython)
        {
            onConsoleOutput?.Invoke(
                ProcessOutput.FromStdOutLine(
                    $"Upgrading venv Python from {requestedPythonVersion} to {targetPythonVersion}"
                )
            );

            ResetVenvForPythonUpgrade(installLocation, onConsoleOutput);
        }

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                forceRecreate: shouldUpgradePython,
                pythonVersion: targetPythonVersion
            )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Running install script...", isIndeterminate: true));

        // Build args for their launch.py - use --uv for fast installs, --exit to quit after setup
        var launchArgs = new List<string> { "launch.py", "--uv", "--exit" };

        // For Ampere or newer GPUs, enable sage attention, flash attention, and nunchaku
        var isAmpereOrNewer =
            SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu()
            ?? HardwareHelper.IterGpuInfo().Any(x => x.IsNvidia && x.IsAmpereOrNewerGpu());

        if (isAmpereOrNewer)
        {
            launchArgs.Add("--sage");
            launchArgs.Add("--flash");
            launchArgs.Add("--nunchaku");
        }

        // Run their install script with our venv Python
        venvRunner.WorkingDirectory = new DirectoryPath(installLocation);

        var sawLegacyUpdatePrompt = false;

        var exitCode = await RunInstallScriptWithPromptHandling(
                venvRunner,
                launchArgs,
                onConsoleOutput,
                cancellationToken,
                onLegacyPromptDetected: () => sawLegacyUpdatePrompt = true
            )
            .ConfigureAwait(false);

        // If legacy prompt was detected, back up old config files regardless of exit code.
        if (options.IsUpdate && sawLegacyUpdatePrompt)
        {
            BackupLegacyConfigFiles(installLocation, onConsoleOutput);

            // If it also failed, retry once after the backup.
            if (exitCode != 0)
            {
                onConsoleOutput?.Invoke(
                    ProcessOutput.FromStdOutLine(
                        "[ForgeClassic] Retrying install after backing up legacy config files..."
                    )
                );

                exitCode = await RunInstallScriptWithPromptHandling(
                        venvRunner,
                        launchArgs,
                        onConsoleOutput,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Install script failed with exit code {exitCode}");
        }

        if (
            !string.Equals(
                installedPackage.PythonVersion,
                targetPythonVersion.StringValue,
                StringComparison.Ordinal
            )
        )
        {
            installedPackage.PythonVersion = targetPythonVersion.StringValue;
        }

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }

    private async Task<int> RunInstallScriptWithPromptHandling(
        IPyVenvRunner venvRunner,
        IReadOnlyCollection<string> launchArgs,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken,
        Action? onLegacyPromptDetected = null
    )
    {
        var enterSent = false;

        void HandleInstallOutput(ProcessOutput output)
        {
            onConsoleOutput?.Invoke(output);

            var isLegacyPrompt =
                output.Text.Contains(LegacyUpgradeAlert, StringComparison.OrdinalIgnoreCase)
                || output.Text.Contains(ContinuePrompt, StringComparison.OrdinalIgnoreCase);

            if (!isLegacyPrompt)
                return;

            onLegacyPromptDetected?.Invoke();

            if (enterSent || venvRunner.Process is null || venvRunner.Process.HasExited)
                return;

            try
            {
                venvRunner.Process.StandardInput.WriteLine();
                enterSent = true;

                onConsoleOutput?.Invoke(
                    ProcessOutput.FromStdOutLine(
                        "[ForgeClassic] Detected legacy update prompt. Sent Enter automatically."
                    )
                );
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to auto-submit Enter for Forge Classic update prompt");
            }
        }

        venvRunner.RunDetached([.. launchArgs], HandleInstallOutput);
        var process =
            venvRunner.Process
            ?? throw new InvalidOperationException("Failed to start Forge Classic install process");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private void ResetVenvForPythonUpgrade(string installLocation, Action<ProcessOutput>? onConsoleOutput)
    {
        var venvPath = Path.Combine(installLocation, "venv");
        if (!Directory.Exists(venvPath))
            return;

        try
        {
            Directory.Delete(venvPath, recursive: true);
            onConsoleOutput?.Invoke(
                ProcessOutput.FromStdOutLine("[ForgeClassic] Removed existing venv before Python upgrade.")
            );
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to remove existing venv during Forge Classic Python upgrade");
            throw new InvalidOperationException(
                "Failed to remove existing venv for Python upgrade. Ensure Forge is not running and retry.",
                e
            );
        }
    }

    private void BackupLegacyConfigFiles(string installLocation, Action<ProcessOutput>? onConsoleOutput)
    {
        BackupLegacyConfigFile(installLocation, "config.json", onConsoleOutput);
        BackupLegacyConfigFile(installLocation, "ui-config.json", onConsoleOutput);
    }

    private void BackupLegacyConfigFile(
        string installLocation,
        string fileName,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        var sourcePath = Path.Combine(installLocation, fileName);
        if (!File.Exists(sourcePath))
            return;

        var backupPath = GetBackupPath(sourcePath);
        File.Move(sourcePath, backupPath);

        var message = $"[ForgeClassic] Backed up {fileName} to {Path.GetFileName(backupPath)}";
        Logger.Info(message);
        onConsoleOutput?.Invoke(ProcessOutput.FromStdOutLine(message));
    }

    private static string GetBackupPath(string sourcePath)
    {
        var nextPath = sourcePath + ".bak";
        if (!File.Exists(nextPath))
            return nextPath;

        var index = 1;
        while (true)
        {
            nextPath = sourcePath + $".bak.{index}";
            if (!File.Exists(nextPath))
                return nextPath;

            index++;
        }
    }

    private async Task InstallTritonAndSageAttention(InstalledPackage? installedPackage)
    {
        if (installedPackage?.FullPath is null)
            return;

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = "Triton and SageAttention installed successfully",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner
            .ExecuteSteps(
                [
                    new ActionPackageStep(
                        async progress =>
                        {
                            await using var venvRunner = await SetupVenvPure(
                                    installedPackage.FullPath,
                                    pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
                                )
                                .ConfigureAwait(false);

                            var gpuInfo =
                                SettingsManager.Settings.PreferredGpu
                                ?? HardwareHelper.IterGpuInfo().FirstOrDefault(x => x.IsNvidia);

                            var tritonVersion = Compat.IsWindows ? "3.5.1.post22" : "3.5.1";

                            await PipWheelService
                                .InstallTritonAsync(venvRunner, progress, tritonVersion)
                                .ConfigureAwait(false);
                            await PipWheelService
                                .InstallSageAttentionAsync(venvRunner, gpuInfo, progress, "2.2.0")
                                .ConfigureAwait(false);
                        },
                        "Installing Triton and SageAttention"
                    ),
                ]
            )
            .ConfigureAwait(false);

        if (runner.Failed)
            return;

        await using var transaction = settingsManager.BeginTransaction();
        var packageInSettings = transaction.Settings.InstalledPackages.FirstOrDefault(x =>
            x.Id == installedPackage.Id
        );

        if (packageInSettings is null)
            return;

        var attentionOptions = packageInSettings.LaunchArgs?.Where(opt =>
            opt.Name.Contains("attention", StringComparison.OrdinalIgnoreCase)
        );

        if (attentionOptions is not null)
        {
            foreach (var option in attentionOptions)
            {
                option.OptionValue = false;
            }
        }

        var sageAttention = packageInSettings.LaunchArgs?.FirstOrDefault(opt =>
            opt.Name.Contains("sage", StringComparison.OrdinalIgnoreCase)
        );

        if (sageAttention is not null)
        {
            sageAttention.OptionValue = true;
        }
        else
        {
            packageInSettings.LaunchArgs?.Add(
                new LaunchOption
                {
                    Name = "--sage",
                    Type = LaunchOptionType.Bool,
                    OptionValue = true,
                }
            );
        }
    }
}
