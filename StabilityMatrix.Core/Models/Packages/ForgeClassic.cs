using Injectio.Attributes;
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
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_11_13;
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
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
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
        venvRunner.RunDetached([.. launchArgs], onConsoleOutput);

        await venvRunner.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (venvRunner.Process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Install script failed with exit code {venvRunner.Process.ExitCode}"
            );
        }

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
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
