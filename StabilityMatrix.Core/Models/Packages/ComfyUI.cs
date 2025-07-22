using System.Text.Json;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, ComfyUI>(Duplicate = DuplicateStrategy.Append)]
public class ComfyUI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => "https://github.com/comfyanonymous/ComfyUI/blob/master/LICENSE";
    public override string Blurb => "A powerful and modular stable diffusion GUI and backend";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/comfyui/preview.webp");
    public override bool IsInferenceCompatible => true;
    public override string OutputFolderName => "output";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.InferenceCompatible;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    // https://github.com/comfyanonymous/ComfyUI/blob/master/folder_paths.py#L11
    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = "extra_model_paths.yaml",
            ConfigFileType = ConfigFileType.Yaml,
            ConfigSharingOptions =
            {
                RootKey = "stability_matrix",
                ConfigDefaultType = ConfigDefaultType.ClearRoot,
            },
            Rules =
            [
                new SharedFolderLayoutRule // Checkpoints
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["models/checkpoints"],
                    ConfigDocumentPaths = ["checkpoints"],
                },
                new SharedFolderLayoutRule // Diffusers
                {
                    SourceTypes = [SharedFolderType.Diffusers],
                    TargetRelativePaths = ["models/diffusers"],
                    ConfigDocumentPaths = ["diffusers"],
                },
                new SharedFolderLayoutRule // Loras
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["models/loras"],
                    ConfigDocumentPaths = ["loras"],
                },
                new SharedFolderLayoutRule // CLIP (Text Encoders)
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["models/clip"],
                    ConfigDocumentPaths = ["clip"],
                },
                new SharedFolderLayoutRule // CLIP Vision
                {
                    SourceTypes = [SharedFolderType.ClipVision],
                    TargetRelativePaths = ["models/clip_vision"],
                    ConfigDocumentPaths = ["clip_vision"],
                },
                new SharedFolderLayoutRule // Embeddings / Textual Inversion
                {
                    SourceTypes = [SharedFolderType.Embeddings],
                    TargetRelativePaths = ["models/embeddings"],
                    ConfigDocumentPaths = ["embeddings"],
                },
                new SharedFolderLayoutRule // VAE
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                    ConfigDocumentPaths = ["vae"],
                },
                new SharedFolderLayoutRule // VAE Approx
                {
                    SourceTypes = [SharedFolderType.ApproxVAE],
                    TargetRelativePaths = ["models/vae_approx"],
                    ConfigDocumentPaths = ["vae_approx"],
                },
                new SharedFolderLayoutRule // ControlNet / T2IAdapter
                {
                    SourceTypes = [SharedFolderType.ControlNet, SharedFolderType.T2IAdapter],
                    TargetRelativePaths = ["models/controlnet"],
                    ConfigDocumentPaths = ["controlnet"],
                },
                new SharedFolderLayoutRule // GLIGEN
                {
                    SourceTypes = [SharedFolderType.GLIGEN],
                    TargetRelativePaths = ["models/gligen"],
                    ConfigDocumentPaths = ["gligen"],
                },
                new SharedFolderLayoutRule // Upscalers
                {
                    SourceTypes =
                    [
                        SharedFolderType.ESRGAN,
                        SharedFolderType.RealESRGAN,
                        SharedFolderType.SwinIR,
                    ],
                    TargetRelativePaths = ["models/upscale_models"],
                    ConfigDocumentPaths = ["upscale_models"],
                },
                new SharedFolderLayoutRule // Hypernetworks
                {
                    SourceTypes = [SharedFolderType.Hypernetwork],
                    TargetRelativePaths = ["models/hypernetworks"],
                    ConfigDocumentPaths = ["hypernetworks"],
                },
                new SharedFolderLayoutRule // IP-Adapter Base, SD1.5, SDXL
                {
                    SourceTypes =
                    [
                        SharedFolderType.IpAdapter,
                        SharedFolderType.IpAdapters15,
                        SharedFolderType.IpAdaptersXl,
                    ],
                    TargetRelativePaths = ["models/ipadapter"], // Single target path
                    ConfigDocumentPaths = ["ipadapter"],
                },
                new SharedFolderLayoutRule // Prompt Expansion
                {
                    SourceTypes = [SharedFolderType.PromptExpansion],
                    TargetRelativePaths = ["models/prompt_expansion"],
                    ConfigDocumentPaths = ["prompt_expansion"],
                },
                new SharedFolderLayoutRule // Ultralytics
                {
                    SourceTypes = [SharedFolderType.Ultralytics], // Might need specific UltralyticsBbox/Segm if symlinks differ
                    TargetRelativePaths = ["models/ultralytics"],
                    ConfigDocumentPaths = ["ultralytics"],
                },
                // Config only rules for Ultralytics bbox/segm
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Ultralytics],
                    SourceSubPath = "bbox",
                    ConfigDocumentPaths = ["ultralytics_bbox"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Ultralytics],
                    SourceSubPath = "segm",
                    ConfigDocumentPaths = ["ultralytics_segm"],
                },
                new SharedFolderLayoutRule // SAMs
                {
                    SourceTypes = [SharedFolderType.Sams],
                    TargetRelativePaths = ["models/sams"],
                    ConfigDocumentPaths = ["sams"],
                },
                new SharedFolderLayoutRule // Diffusion Models / Unet
                {
                    SourceTypes = [SharedFolderType.DiffusionModels],
                    TargetRelativePaths = ["models/diffusion_models"],
                    ConfigDocumentPaths = ["diffusion_models"],
                },
            ],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = ["output"] };

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--listen"],
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "8188",
                Options = ["--port"],
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--lowvram",
                    MemoryLevel.Medium => "--normalvram",
                    _ => null,
                },
                Options = ["--highvram", "--normalvram", "--lowvram", "--novram"],
            },
            new()
            {
                Name = "Reserve VRAM",
                Type = LaunchOptionType.String,
                InitialValue = Compat.IsWindows && HardwareHelper.HasAmdGpu() ? "0.9" : null,
                Description =
                    "Sets the amount of VRAM (in GB) you want to reserve for use by your OS/other software",
                Options = ["--reserve-vram"],
            },
            new()
            {
                Name = "Preview Method",
                Type = LaunchOptionType.Bool,
                InitialValue = "--preview-method auto",
                Options = ["--preview-method auto", "--preview-method latent2rgb", "--preview-method taesd"],
            },
            new()
            {
                Name = "Enable DirectML",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectMLOrZluda() && this is not ComfyZluda,
                Options = ["--directml"],
            },
            new()
            {
                Name = "Use CPU only",
                Type = LaunchOptionType.Bool,
                InitialValue =
                    !Compat.IsMacOS && !HardwareHelper.HasNvidiaGpu() && !HardwareHelper.HasAmdGpu(),
                Options = ["--cpu"],
            },
            new()
            {
                Name = "Cross Attention Method",
                Type = LaunchOptionType.Bool,
                InitialValue = "--use-pytorch-cross-attention",
                Options =
                [
                    "--use-split-cross-attention",
                    "--use-quad-cross-attention",
                    "--use-pytorch-cross-attention",
                    "--use-sage-attention",
                ],
            },
            new()
            {
                Name = "Force Floating Point Precision",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS ? "--force-fp16" : null,
                Options = ["--force-fp32", "--force-fp16"],
            },
            new()
            {
                Name = "VAE Precision",
                Type = LaunchOptionType.Bool,
                Options = ["--fp16-vae", "--fp32-vae", "--bf16-vae"],
            },
            new()
            {
                Name = "Disable Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = ["--disable-xformers"],
            },
            new()
            {
                Name = "Disable upcasting of attention",
                Type = LaunchOptionType.Bool,
                Options = ["--dont-upcast-attention"],
            },
            new()
            {
                Name = "Auto-Launch",
                Type = LaunchOptionType.Bool,
                Options = ["--auto-launch"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override string MainBranch => "master";

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps];

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

        if (!Compat.IsMacOS && SettingsManager.Settings.PreferredGpu?.ComputeCapabilityValue is >= 7.5m)
        {
            commands.Add(
                new ExtraPackageCommand { CommandName = "Install Nunchaku", Command = InstallNunchaku }
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
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var isLegacyNvidia =
            torchVersion == TorchIndex.Cuda
            && (
                SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu()
                ?? HardwareHelper.HasLegacyNvidiaGpu()
            );

        var pipArgs = new PipInstallArgs();

        pipArgs = torchVersion switch
        {
            TorchIndex.DirectMl => pipArgs.WithTorchDirectML(),
            _ => pipArgs
                .AddArg("--upgrade")
                .WithTorch()
                .WithTorchVision()
                .WithTorchAudio()
                .WithTorchExtraIndex(
                    torchVersion switch
                    {
                        TorchIndex.Cpu => "cpu",
                        TorchIndex.Cuda when isLegacyNvidia => "cu126",
                        TorchIndex.Cuda => "cu128",
                        TorchIndex.Rocm => "rocm6.3",
                        TorchIndex.Mps => "cpu",
                        _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null),
                    }
                ),
        };

        var requirements = new FilePath(installLocation, "requirements.txt");

        pipArgs = pipArgs.WithParsedFromRequirementsTxt(
            await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
            excludePattern: "torch$|numpy"
        );

        // https://github.com/comfyanonymous/ComfyUI/pull/4121
        // https://github.com/comfyanonymous/ComfyUI/commit/e6829e7ac5bef5db8099005b5b038c49e173e87c
        pipArgs = pipArgs.AddArg("numpy<2");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installed Package Requirements", isIndeterminate: false));
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        // Use the same Python version that was used for installation
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. options.Arguments],
            HandleConsoleOutput,
            OnExit
        );

        if (Compat.IsWindows)
        {
            ProcessTracker.AttachExitHandlerJobToProcess(VenvRunner.Process);
        }

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("To see the GUI go to", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (match.Success)
            {
                WebUrl = match.Value;
            }
            OnStartupComplete(WebUrl);
        }
    }

    public override IPackageExtensionManager ExtensionManager =>
        new ComfyExtensionManager(this, settingsManager);

    private class ComfyExtensionManager(ComfyUI package, ISettingsManager settingsManager)
        : GitPackageExtensionManager(package.PrerequisiteHelper)
    {
        public override string RelativeInstallDirectory => "custom_nodes";

        public override IEnumerable<ExtensionManifest> DefaultManifests =>
            [
                "https://cdn.jsdelivr.net/gh/ltdrdata/ComfyUI-Manager/custom-node-list.json",
                "https://cdn.jsdelivr.net/gh/LykosAI/ComfyUI-Extensions-Index/custom-node-list.json",
            ];

        public override async Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
            ExtensionManifest manifest,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                // Get json
                var content = await package
                    .DownloadService.GetContentAsync(manifest.Uri.ToString(), cancellationToken)
                    .ConfigureAwait(false);

                // Parse json
                var jsonManifest = JsonSerializer.Deserialize<ComfyExtensionManifest>(
                    content,
                    ComfyExtensionManifestSerializerContext.Default.Options
                );

                if (jsonManifest == null)
                    return [];

                var extensions = jsonManifest.GetPackageExtensions().ToList();
                return extensions;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get package extensions");
                return [];
            }
        }

        /// <inheritdoc />
        public override async Task UpdateExtensionAsync(
            InstalledPackageExtension installedExtension,
            InstalledPackage installedPackage,
            PackageExtensionVersion? version = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            await base.UpdateExtensionAsync(
                    installedExtension,
                    installedPackage,
                    version,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var installedDirs = installedExtension.Paths.OfType<DirectoryPath>().Where(dir => dir.Exists);

            await PostInstallAsync(
                    installedPackage,
                    installedDirs,
                    installedExtension.Definition!,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task InstallExtensionAsync(
            PackageExtension extension,
            InstalledPackage installedPackage,
            PackageExtensionVersion? version = null,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            await base.InstallExtensionAsync(
                    extension,
                    installedPackage,
                    version,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var cloneRoot = new DirectoryPath(installedPackage.FullPath!, RelativeInstallDirectory);

            var installedDirs = extension
                .Files.Select(uri => uri.Segments.LastOrDefault())
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => cloneRoot.JoinDir(path!))
                .Where(dir => dir.Exists);

            await PostInstallAsync(installedPackage, installedDirs, extension, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Runs post install / update tasks (i.e. install.py, requirements.txt)
        /// </summary>
        private async Task PostInstallAsync(
            InstalledPackage installedPackage,
            IEnumerable<DirectoryPath> installedDirs,
            PackageExtension extension,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            // do pip installs
            if (extension.Pip != null)
            {
                await using var venvRunner = await package
                    .SetupVenvPure(
                        installedPackage.FullPath!,
                        pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
                    )
                    .ConfigureAwait(false);

                var pipArgs = new PipInstallArgs();
                pipArgs = extension.Pip.Aggregate(pipArgs, (current, pip) => current.AddArg(pip));

                await venvRunner
                    .PipInstall(pipArgs, progress?.AsProcessOutputHandler())
                    .ConfigureAwait(false);
            }

            foreach (var installedDir in installedDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Install requirements.txt if found
                if (installedDir.JoinFile("requirements.txt") is { Exists: true } requirementsFile)
                {
                    var requirementsContent = await requirementsFile
                        .ReadAllTextAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(requirementsContent))
                    {
                        progress?.Report(
                            new ProgressReport(
                                0f,
                                $"Installing requirements.txt for {installedDir.Name}",
                                isIndeterminate: true
                            )
                        );

                        await using var venvRunner = await package
                            .SetupVenvPure(
                                installedPackage.FullPath!,
                                pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
                            )
                            .ConfigureAwait(false);

                        var pipArgs = new PipInstallArgs().WithParsedFromRequirementsTxt(requirementsContent);

                        await venvRunner
                            .PipInstall(pipArgs, progress.AsProcessOutputHandler())
                            .ConfigureAwait(false);

                        progress?.Report(
                            new ProgressReport(1f, $"Installed requirements.txt for {installedDir.Name}")
                        );
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Run install.py if found
                if (installedDir.JoinFile("install.py") is { Exists: true } installScript)
                {
                    progress?.Report(
                        new ProgressReport(
                            0f,
                            $"Running install.py for {installedDir.Name}",
                            isIndeterminate: true
                        )
                    );

                    await using var venvRunner = await package
                        .SetupVenvPure(
                            installedPackage.FullPath!,
                            pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
                        )
                        .ConfigureAwait(false);

                    venvRunner.WorkingDirectory = installScript.Directory;
                    venvRunner.UpdateEnvironmentVariables(env =>
                    {
                        // set env vars for Impact Pack for Face Detailer
                        env = env.SetItem("COMFYUI_PATH", installedPackage.FullPath!);

                        var modelPath =
                            installedPackage.PreferredSharedFolderMethod == SharedFolderMethod.None
                                ? Path.Combine(installedPackage.FullPath!, "models")
                                : settingsManager.ModelsDirectory;

                        env = env.SetItem("COMFYUI_MODEL_PATH", modelPath);
                        return env;
                    });

                    venvRunner.RunDetached(["install.py"], progress.AsProcessOutputHandler());

                    await venvRunner.Process.WaitUntilOutputEOF(cancellationToken).ConfigureAwait(false);
                    await venvRunner.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (venvRunner.Process.HasExited && venvRunner.Process.ExitCode != 0)
                    {
                        throw new ProcessException(
                            $"install.py for {installedDir.Name} exited with code {venvRunner.Process.ExitCode}"
                        );
                    }

                    progress?.Report(new ProgressReport(1f, $"Ran launch.py for {installedDir.Name}"));
                }
            }
        }
    }

    private async Task InstallTritonAndSageAttention(InstalledPackage? installedPackage)
    {
        if (installedPackage?.FullPath is null)
            return;

        var installSageStep = new InstallSageAttentionStep(
            DownloadService,
            PrerequisiteHelper,
            PyInstallationManager
        )
        {
            InstalledPackage = installedPackage,
            WorkingDirectory = new DirectoryPath(installedPackage.FullPath),
            EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables,
            IsBlackwellGpu =
                SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu(),
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = "Triton and SageAttention installed successfully",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps([installSageStep]).ConfigureAwait(false);

        if (runner.Failed)
            return;

        await using var transaction = settingsManager.BeginTransaction();
        var attentionOptions = transaction
            .Settings.InstalledPackages.First(x => x.Id == installedPackage.Id)
            .LaunchArgs?.Where(opt => opt.Name.Contains("attention"));

        if (attentionOptions is not null)
        {
            foreach (var option in attentionOptions)
            {
                option.OptionValue = false;
            }
        }

        var sageAttention = transaction
            .Settings.InstalledPackages.First(x => x.Id == installedPackage.Id)
            .LaunchArgs?.FirstOrDefault(opt => opt.Name.Contains("sage-attention"));

        if (sageAttention is not null)
        {
            sageAttention.OptionValue = true;
        }
        else
        {
            transaction
                .Settings.InstalledPackages.First(x => x.Id == installedPackage.Id)
                .LaunchArgs?.Add(
                    new LaunchOption
                    {
                        Name = "--use-sage-attention",
                        Type = LaunchOptionType.Bool,
                        OptionValue = true,
                    }
                );
        }
    }

    private async Task InstallNunchaku(InstalledPackage? installedPackage)
    {
        if (installedPackage?.FullPath is null)
            return;

        var installNunchaku = new InstallNunchakuStep(
            DownloadService,
            PrerequisiteHelper,
            PyInstallationManager
        )
        {
            InstalledPackage = installedPackage,
            WorkingDirectory = new DirectoryPath(installedPackage.FullPath),
            EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables,
            PreferredGpu =
                SettingsManager.Settings.PreferredGpu
                ?? HardwareHelper.IterGpuInfo().FirstOrDefault(x => x.IsNvidia || x.IsAmd),
            ComfyExtensionManager = ExtensionManager,
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = "Nunchaku installed successfully",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps([installNunchaku]).ConfigureAwait(false);
    }
}
