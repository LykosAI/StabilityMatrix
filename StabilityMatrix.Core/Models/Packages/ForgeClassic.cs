﻿using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
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
    IPrerequisiteHelper prerequisiteHelper
) : SDWebForge(githubApi, settingsManager, downloadService, prerequisiteHelper)
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
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Recommended;
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--server-name"]
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"]
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" }
            },
            new()
            {
                Name = "Xformers",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to use xformers",
                Options = { "--xformers" }
            },
            new()
            {
                Name = "Use SageAttention",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to use sage attention",
                Options = { "--sage" }
            },
            new()
            {
                Name = "Pin Shared Memory",
                Type = LaunchOptionType.Bool,
                Options = { "--pin-shared-memory" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false
            },
            new()
            {
                Name = "CUDA Malloc",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-malloc" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false
            },
            new()
            {
                Name = "CUDA Stream",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-stream" },
                InitialValue = SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() ?? false
            },
            new()
            {
                Name = "Auto Launch",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to auto launch the webui",
                Options = { "--auto-launch" }
            },
            new()
            {
                Name = "Skip Python Version Check",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to skip python version check",
                Options = { "--skip-python-version-check" },
                InitialValue = true
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

        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

        var requirements = new FilePath(installLocation, "requirements.txt");
        var requirementsContent = await requirements
            .ReadAllTextAsync(cancellationToken)
            .ConfigureAwait(false);

        var pipArgs = new PipInstallArgs()
            .AddArg("--upgrade")
            .WithTorch()
            .WithTorchVision()
            .WithTorchAudio()
            .WithTorchExtraIndex("cu128");

        pipArgs = pipArgs.WithParsedFromRequirementsTxt(requirementsContent, excludePattern: "torch");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
