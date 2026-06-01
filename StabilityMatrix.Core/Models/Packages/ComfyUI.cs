using System.Collections.Immutable;
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
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, ComfyUI>(Duplicate = DuplicateStrategy.Append)]
public class ComfyUI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService,
    IRocmPackageHelper rocmPackageHelper
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
                    _ => null,
                },
                Options = ["--highvram", "--lowvram", "--novram"],
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
                InitialValue =
                    !HasWindowsRocmSupport()
                    && HardwareHelper.PreferDirectMLOrZluda()
                    && this is not ComfyZluda,
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
                InitialValue = DefaultToQuadCrossAttention()
                    ? "--use-quad-cross-attention" // For Legacy AMD GPUs.
                    : "--use-pytorch-cross-attention",
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
            new()
            {
                Name = "Enable Manager",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = ["--enable-manager"],
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

        if (Compat.IsWindows && HasWindowsRocmSupport())
        {
            commands.Add(
                new ExtraPackageCommand
                {
                    CommandName = "Install Triton and SageAttention (ROCm)",
                    Command = InstallWindowsRocmSageAttention,
                }
            );

            commands.Add(
                new ExtraPackageCommand
                {
                    CommandName = "Install Flash Attention (ROCm)",
                    Command = InstallWindowsRocmFlashAttention,
                    IsVisible = _ =>
                        WindowsRocmSupport.IsLegacyArchitecture(
                            GetWindowsRocmCompatibility().ResolvedGfxArch
                        ),
                }
            );

            commands.Add(
                new ExtraPackageCommand
                {
                    CommandName = "Install ROCm Development SDK",
                    Command = InstallWindowsRocmDevelopmentSdk,
                }
            );

            commands.Add(
                new ExtraPackageCommand
                {
                    CommandName = "Install bitsandbytes (ROCm)",
                    Command = InstallWindowsRocmBitsAndBytes,
                    IsVisible = installedPackage =>
                    {
                        if (!PyVersion.TryParse(installedPackage.PythonVersion, out var pyVersion))
                            return false;

                        return pyVersion.Major == 3 && pyVersion.Minor == 12;
                    },
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

        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var isLegacyNvidia =
            torchIndex == TorchIndex.Cuda
            && (
                SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu()
                ?? HardwareHelper.HasLegacyNvidiaGpu()
            );

        if (Compat.IsWindows && torchIndex == TorchIndex.Rocm && HasWindowsRocmSupport())
        {
            var config = rocmPackageHelper.BuildWindowsNativeInstallConfig(ComfyWindowsRocmProfile.Profile);

            await StandardPipInstallProcessAsync(
                    venvRunner,
                    options,
                    installedPackage,
                    config,
                    onConsoleOutput,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);

            await rocmPackageHelper
                .InstallWindowsNativeTorchAsync(
                    venvRunner,
                    installedPackage,
                    ComfyWindowsRocmProfile.Profile,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            var config = new PipInstallConfig
            {
                RequirementsFilePaths = ["requirements.txt"],
                ExtraPipArgs = ["numpy<2"],
                TorchaudioVersion = " ", // Request torchaudio without a specific version
                CudaIndex = isLegacyNvidia ? "cu126" : "cu130",
                RocmIndex = "rocm7.2",
                UpgradePackages = true,
                PostInstallPipArgs = ["typing-extensions>=4.15.0"],
            };

            await StandardPipInstallProcessAsync(
                    venvRunner,
                    options,
                    installedPackage,
                    config,
                    onConsoleOutput,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (!(Compat.IsWindows && torchIndex == TorchIndex.Rocm && HasWindowsRocmSupport()))
        {
            try
            {
                var sageVersion = await venvRunner.PipShow("sageattention").ConfigureAwait(false);
                var torchVersion = await venvRunner.PipShow("torch").ConfigureAwait(false);

                if (torchVersion is not null && sageVersion is not null)
                {
                    var version = torchVersion.Version;
                    var plusPos = version.IndexOf('+');
                    var index = plusPos >= 0 ? version[(plusPos + 1)..] : string.Empty;
                    var versionWithoutIndex = plusPos >= 0 ? version[..plusPos] : version;

                    if (
                        !sageVersion.Version.Contains(index)
                        || !sageVersion.Version.Contains(versionWithoutIndex)
                    )
                    {
                        progress?.Report(
                            new ProgressReport(-1f, "Updating SageAttention...", isIndeterminate: true)
                        );

                        var step = new InstallSageAttentionStep(
                            downloadService,
                            prerequisiteHelper,
                            pyInstallationManager
                        )
                        {
                            InstalledPackage = installedPackage,
                            IsBlackwellGpu =
                                SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu()
                                ?? HardwareHelper.HasBlackwellGpu(),
                            WorkingDirectory = installLocation,
                            EnvironmentVariables = GetEnvVars(
                                venvRunner.EnvironmentVariables,
                                installLocation,
                                installedPackage
                            ),
                        };

                        await step.ExecuteAsync(progress).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to verify/update SageAttention after installation");
            }
        }

        // Install Comfy Manager (built-in to ComfyUI)
        try
        {
            var managerRequirementsFile = Path.Combine(installLocation, "manager_requirements.txt");
            if (File.Exists(managerRequirementsFile))
            {
                progress?.Report(
                    new ProgressReport(-1f, "Installing Comfy Manager requirements...", isIndeterminate: true)
                );

                var pipArgs = new PipInstallArgs().AddArg("-r").AddArg(managerRequirementsFile);
                await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

                progress?.Report(
                    new ProgressReport(-1f, "Comfy Manager installed successfully", isIndeterminate: true)
                );
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to install Comfy Manager requirements");
        }

        progress?.Report(new ProgressReport(1, "Install complete", isIndeterminate: false));
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

        VenvRunner.UpdateEnvironmentVariables(env => GetEnvVars(env, installLocation, installedPackage));
        var launchArguments = NormalizeLaunchArguments(installedPackage, options.Arguments);

        // Check for old NVIDIA driver version with cu130 installations
        var isNvidia = SettingsManager.Settings.PreferredGpu?.IsNvidia ?? HardwareHelper.HasNvidiaGpu();
        var isLegacyNvidia =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();

        if (isNvidia && !isLegacyNvidia)
        {
            var driverVersion = HardwareHelper.GetNvidiaDriverVersion();
            if (driverVersion is not null && driverVersion.Major < 580)
            {
                // Check if torch is installed with cu130 index
                var torchInfo = await VenvRunner.PipShow("torch").ConfigureAwait(false);
                if (torchInfo is not null)
                {
                    var version = torchInfo.Version;
                    var plusPos = version.IndexOf('+');
                    var torchIndex = plusPos >= 0 ? version[(plusPos + 1)..] : string.Empty;

                    // Only warn if using cu130 (which requires driver 580+)
                    if (torchIndex.Equals("cu130", StringComparison.OrdinalIgnoreCase))
                    {
                        var warningMessage = $"""

                            ============================================================
                                            NVIDIA DRIVER WARNING
                            ============================================================

                            Your NVIDIA driver version ({driverVersion}) is older than
                            the minimum required version (580.x) for CUDA 13.0 (cu130).

                            This may cause ComfyUI to fail to start or experience issues.

                            Recommended actions:
                              1. Update your NVIDIA driver to version 580 or newer
                              2. Or manually downgrade your torch version to use an
                                 older torch index (e.g. cu128)

                            ============================================================

                            """;

                        Logger.Warn(
                            "NVIDIA driver version {DriverVersion} is below 580.x minimum for cu130 (torch index: {TorchIndex})",
                            driverVersion,
                            torchIndex
                        );
                        onConsoleOutput?.Invoke(ProcessOutput.FromStdErrLine(warningMessage));
                        return;
                    }
                }
            }
        }

        var handledFirstConsoleOutput = false;

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. launchArguments],
            HandleConsoleOutput,
            OnExit
        );

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!handledFirstConsoleOutput)
            {
                handledFirstConsoleOutput = true;
                EmitWindowsRocmLaunchNotice(installedPackage, onConsoleOutput);
            }

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

    private void EmitWindowsRocmLaunchNotice(
        InstalledPackage installedPackage,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        var torchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();
        foreach (var line in rocmPackageHelper.GetWindowsLaunchNoticeLines(torchIndex))
        {
            onConsoleOutput?.Invoke(ProcessOutput.FromStdOutLine($"{line}{Environment.NewLine}"));
        }
    }

    protected ProcessArgs NormalizeLaunchArguments(
        InstalledPackage installedPackage,
        ProcessArgs fallbackArguments
    )
    {
        if (installedPackage.LaunchArgs is not { Count: > 0 })
        {
            return fallbackArguments;
        }

        var removedCount = installedPackage.LaunchArgs.RemoveAll(option =>
            string.Equals(option.Name, "--normalvram", StringComparison.OrdinalIgnoreCase)
        );

        if (removedCount == 0)
        {
            return fallbackArguments;
        }

        Logger.Info("Removed {RemovedCount} obsolete ComfyUI launch args before launch", removedCount);

        SettingsManager.SaveLaunchArgs(installedPackage.Id, installedPackage.LaunchArgs);

        return ProcessArgs.FromQuoted(
            installedPackage.LaunchArgs.Select(option => option.ToArgString()).OfType<string>()
        );
    }

    public override TorchIndex GetRecommendedTorchVersion()
    {
        var preferRocm =
            (Compat.IsLinux && (SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.PreferRocm()))
            || HasWindowsRocmSupport();

        if (AvailableTorchIndices.Contains(TorchIndex.Rocm) && preferRocm)
        {
            return TorchIndex.Rocm;
        }

        return base.GetRecommendedTorchVersion();
    }

    /// Uses the shared ROCm helper for Windows ROCm eligibility checks so ComfyUI does not maintain its own support matrix.
    private bool HasWindowsRocmSupport()
    {
        return HasWindowsRocmSupport(rocmPackageHelper);
    }

    private RocmCompatibilityResult GetWindowsRocmCompatibility()
    {
        return GetWindowsRocmCompatibility(rocmPackageHelper);
    }

    /// Defaults legacy Windows ROCm GPUs to quad cross-attention because PyTorch cross-attention is considerably slower
    /// and not as supported on older AMD architectures.
    private bool DefaultToQuadCrossAttention()
    {
        var compatibility = GetWindowsRocmCompatibility();
        if (!compatibility.IsCompatible)
            return false;

        return WindowsRocmSupport.PreferLegacyAttentionFallback(compatibility.ResolvedGfxArch);
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

                            await PipWheelService
                                .InstallTritonAsync(venvRunner, progress)
                                .ConfigureAwait(false);
                            await PipWheelService
                                .InstallSageAttentionAsync(venvRunner, gpuInfo, progress)
                                .ConfigureAwait(false);
                        },
                        "Installing Triton and SageAttention"
                    ),
                ]
            )
            .ConfigureAwait(false);

        if (runner.Failed)
            return;

        await EnableSageAttentionAsync(installedPackage).ConfigureAwait(false);
    }

    private async Task InstallWindowsRocmSageAttention(InstalledPackage? installedPackage)
    {
        var succeeded = await RunWindowsRocmPackageCommandAsync(
                installedPackage,
                WindowsRocmPackageCommandType.SageAttention,
                "Windows ROCm SageAttention installed successfully",
                includeEnvironmentVariables: true
            )
            .ConfigureAwait(false);

        if (!succeeded || installedPackage is null)
            return;

        await EnableSageAttentionAsync(installedPackage).ConfigureAwait(false);
    }

    private async Task InstallWindowsRocmDevelopmentSdk(InstalledPackage? installedPackage)
    {
        await RunWindowsRocmPackageCommandAsync(
                installedPackage,
                WindowsRocmPackageCommandType.DevelopmentSdk,
                "Windows ROCm Development SDK installed successfully",
                includeEnvironmentVariables: false
            )
            .ConfigureAwait(false);
    }

    private async Task InstallWindowsRocmBitsAndBytes(InstalledPackage? installedPackage)
    {
        await RunWindowsRocmPackageCommandAsync(
                installedPackage,
                WindowsRocmPackageCommandType.BitsAndBytes,
                "Windows ROCm bitsandbytes installed successfully",
                includeEnvironmentVariables: true
            )
            .ConfigureAwait(false);
    }

    private async Task InstallWindowsRocmFlashAttention(InstalledPackage? installedPackage)
    {
        await RunWindowsRocmPackageCommandAsync(
                installedPackage,
                WindowsRocmPackageCommandType.FlashAttention,
                "Windows ROCm Flash Attention installed successfully",
                includeEnvironmentVariables: true
            )
            .ConfigureAwait(false);
    }

    private async Task<bool> RunWindowsRocmPackageCommandAsync(
        InstalledPackage? installedPackage,
        WindowsRocmPackageCommandType commandType,
        string completionMessage,
        bool includeEnvironmentVariables
    )
    {
        if (installedPackage?.FullPath is null)
            return false;

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = completionMessage,
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        IReadOnlyDictionary<string, string>? environmentVariables = null;
        if (includeEnvironmentVariables)
        {
            var baseEnvironment = ImmutableDictionary.CreateRange(
                SettingsManager.Settings.EnvironmentVariables
            );
            environmentVariables = GetEnvVars(baseEnvironment, installedPackage.FullPath, installedPackage);
        }

        await runner
            .ExecuteSteps(
                [
                    new InstallWindowsRocmPackageCommandStep(
                        downloadService,
                        pyInstallationManager,
                        prerequisiteHelper,
                        rocmPackageHelper
                    )
                    {
                        CommandType = commandType,
                        InstalledPackage = installedPackage,
                        WorkingDirectory = new DirectoryPath(installedPackage.FullPath),
                        EnvironmentVariables = environmentVariables,
                    },
                ]
            )
            .ConfigureAwait(false);

        return !runner.Failed;
    }

    private async Task EnableSageAttentionAsync(InstalledPackage installedPackage)
    {
        await using var transaction = settingsManager.BeginTransaction();
        var packageInSettings = transaction.Settings.InstalledPackages.First(x =>
            x.Id == installedPackage.Id
        );

        var attentionOptions = packageInSettings.LaunchArgs?.Where(opt => opt.Name.Contains("attention"));
        if (attentionOptions is not null)
        {
            foreach (var option in attentionOptions)
            {
                option.OptionValue = false;
            }
        }

        var sageAttention = packageInSettings.LaunchArgs?.FirstOrDefault(opt =>
            opt.Name.Contains("sage-attention")
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

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = "Nunchaku installed successfully",
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
                                ?? HardwareHelper.IterGpuInfo().FirstOrDefault(x => x.IsNvidia || x.IsAmd);

                            await PipWheelService
                                .InstallNunchakuAsync(venvRunner, gpuInfo, progress)
                                .ConfigureAwait(false);
                        },
                        "Installing Nunchaku"
                    ),
                ]
            )
            .ConfigureAwait(false);
    }

    private ImmutableDictionary<string, string> GetEnvVars(
        ImmutableDictionary<string, string> env,
        string installLocation,
        InstalledPackage installedPackage
    )
    {
        // if we're not on windows or we don't have a windows rocm gpu, return original env
        var hasRocmGpu = HasWindowsRocmSupport();
        var selectedTorchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();

        if (!Compat.IsWindows || !hasRocmGpu || selectedTorchIndex != TorchIndex.Rocm)
            return env;

        var rocmEnvironment = rocmPackageHelper.BuildLaunchEnvironment(ComfyWindowsRocmProfile.Profile);

        return env.SetItems(rocmEnvironment);
    }
}
