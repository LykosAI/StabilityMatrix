using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class ComfyUI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => "https://github.com/comfyanonymous/ComfyUI/blob/master/LICENSE";
    public override string Blurb => "A powerful and modular stable diffusion GUI and backend";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/comfyanonymous/ComfyUI/raw/master/comfyui_screenshot.png");
    public override bool ShouldIgnoreReleases => true;
    public override bool IsInferenceCompatible => true;
    public override string OutputFolderName => "output";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.InferenceCompatible;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;

    // https://github.com/comfyanonymous/ComfyUI/blob/master/folder_paths.py#L11
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = ["models/checkpoints"],
            [SharedFolderType.Diffusers] = ["models/diffusers"],
            [SharedFolderType.Lora] = ["models/loras"],
            [SharedFolderType.CLIP] = ["models/clip"],
            [SharedFolderType.InvokeClipVision] = ["models/clip_vision"],
            [SharedFolderType.TextualInversion] = ["models/embeddings"],
            [SharedFolderType.VAE] = ["models/vae"],
            [SharedFolderType.ApproxVAE] = ["models/vae_approx"],
            [SharedFolderType.ControlNet] = ["models/controlnet/ControlNet"],
            [SharedFolderType.GLIGEN] = ["models/gligen"],
            [SharedFolderType.ESRGAN] = ["models/upscale_models"],
            [SharedFolderType.Hypernetwork] = ["models/hypernetworks"],
            [SharedFolderType.IpAdapter] = ["models/ipadapter/base"],
            [SharedFolderType.InvokeIpAdapters15] = ["models/ipadapter/sd15"],
            [SharedFolderType.InvokeIpAdaptersXl] = ["models/ipadapter/sdxl"],
            [SharedFolderType.T2IAdapter] = ["models/controlnet/T2IAdapter"],
            [SharedFolderType.PromptExpansion] = ["models/prompt_expansion"],
            [SharedFolderType.Ultralytics] = ["models/ultralytics"],
            [SharedFolderType.Sams] = ["models/sams"],
            [SharedFolderType.Unet] = ["models/unet"]
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = ["output"] };

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new LaunchOptionDefinition
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--listen"]
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "8188",
                Options = ["--port"]
            },
            new LaunchOptionDefinition
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--lowvram",
                    MemoryLevel.Medium => "--normalvram",
                    _ => null
                },
                Options = ["--highvram", "--normalvram", "--lowvram", "--novram"]
            },
            new LaunchOptionDefinition
            {
                Name = "Preview Method",
                Type = LaunchOptionType.Bool,
                InitialValue = "--preview-method auto",
                Options = ["--preview-method auto", "--preview-method latent2rgb", "--preview-method taesd"]
            },
            new LaunchOptionDefinition
            {
                Name = "Enable DirectML",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectML(),
                Options = ["--directml"]
            },
            new LaunchOptionDefinition
            {
                Name = "Use CPU only",
                Type = LaunchOptionType.Bool,
                InitialValue =
                    !Compat.IsMacOS && !HardwareHelper.HasNvidiaGpu() && !HardwareHelper.HasAmdGpu(),
                Options = ["--cpu"]
            },
            new LaunchOptionDefinition
            {
                Name = "Cross Attention Method",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS ? "--use-pytorch-cross-attention" : null,
                Options =
                [
                    "--use-split-cross-attention",
                    "--use-quad-cross-attention",
                    "--use-pytorch-cross-attention"
                ]
            },
            new LaunchOptionDefinition
            {
                Name = "Force Floating Point Precision",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS ? "--force-fp16" : null,
                Options = ["--force-fp32", "--force-fp16"]
            },
            new LaunchOptionDefinition
            {
                Name = "VAE Precision",
                Type = LaunchOptionType.Bool,
                Options = ["--fp16-vae", "--fp32-vae", "--bf16-vae"]
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = ["--disable-xformers"]
            },
            new LaunchOptionDefinition
            {
                Name = "Disable upcasting of attention",
                Type = LaunchOptionType.Bool,
                Options = ["--dont-upcast-attention"]
            },
            new LaunchOptionDefinition
            {
                Name = "Auto-Launch",
                Type = LaunchOptionType.Bool,
                Options = ["--auto-launch"]
            },
            LaunchOptionDefinition.Extras
        ];

    public override string MainBranch => "master";

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        [TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.DirectMl, TorchVersion.Rocm, TorchVersion.Mps];

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
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        var torchVersion = options.PythonOptions.TorchVersion ?? GetRecommendedTorchVersion();

        var pipArgs = new PipInstallArgs();

        pipArgs = torchVersion switch
        {
            TorchVersion.DirectMl => pipArgs.WithTorchDirectML(),
            _
                => pipArgs
                    .AddArg("--upgrade")
                    .WithTorch()
                    .WithTorchVision()
                    .WithTorchExtraIndex(
                        torchVersion switch
                        {
                            TorchVersion.Cpu => "cpu",
                            TorchVersion.Cuda => "cu121",
                            TorchVersion.Rocm => "rocm6.0",
                            TorchVersion.Mps => "cpu",
                            _
                                => throw new ArgumentOutOfRangeException(
                                    nameof(torchVersion),
                                    torchVersion,
                                    null
                                )
                        }
                    )
        };

        var requirements = new FilePath(installLocation, "requirements.txt");

        pipArgs = pipArgs.WithParsedFromRequirementsTxt(
            await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
            excludePattern: "torch|numpy"
        );

        // https://github.com/comfyanonymous/ComfyUI/pull/4121
        // https://github.com/comfyanonymous/ComfyUI/commit/e6829e7ac5bef5db8099005b5b038c49e173e87c
        pipArgs = pipArgs.AddArg("numpy<2");

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
        await SetupVenv(installLocation).ConfigureAwait(false);

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
            HandleConsoleOutput,
            OnExit
        );

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

    public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink
                => base.SetupModelFolders(installDirectory, SharedFolderMethod.Symlink),
            SharedFolderMethod.Configuration => SetupModelFoldersConfig(installDirectory),
            SharedFolderMethod.None => Task.CompletedTask,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedFolderMethod), sharedFolderMethod, null)
        };

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        return sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink => base.RemoveModelFolderLinks(installDirectory, sharedFolderMethod),
            SharedFolderMethod.Configuration => RemoveConfigSection(installDirectory),
            SharedFolderMethod.None => Task.CompletedTask,
            _ => throw new ArgumentOutOfRangeException(nameof(sharedFolderMethod), sharedFolderMethod, null)
        };
    }

    private async Task SetupModelFoldersConfig(DirectoryPath installDirectory)
    {
        var extraPathsYamlPath = installDirectory.JoinFile("extra_model_paths.yaml");
        var modelsDir = SettingsManager.ModelsDirectory;

        if (!extraPathsYamlPath.Exists)
        {
            Logger.Info("Creating extra_model_paths.yaml");
            extraPathsYamlPath.Create();
        }

        var yaml = await extraPathsYamlPath.ReadAllTextAsync().ConfigureAwait(false);
        using var sr = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(sr);

        if (!yamlStream.Documents.Any())
        {
            yamlStream.Documents.Add(new YamlDocument(new YamlMappingNode()));
        }

        var root = yamlStream.Documents[0].RootNode;
        if (root is not YamlMappingNode mappingNode)
        {
            throw new Exception("Invalid extra_model_paths.yaml");
        }
        // check if we have a child called "stability_matrix"
        var stabilityMatrixNode = mappingNode.Children.FirstOrDefault(
            c => c.Key.ToString() == "stability_matrix"
        );

        if (stabilityMatrixNode.Key != null)
        {
            if (stabilityMatrixNode.Value is not YamlMappingNode nodeValue)
                return;

            nodeValue.Children["checkpoints"] = Path.Combine(modelsDir, "StableDiffusion");
            nodeValue.Children["vae"] = Path.Combine(modelsDir, "VAE");
            nodeValue.Children["loras"] =
                $"{Path.Combine(modelsDir, "Lora")}\n" + $"{Path.Combine(modelsDir, "LyCORIS")}";
            nodeValue.Children["upscale_models"] =
                $"{Path.Combine(modelsDir, "ESRGAN")}\n"
                + $"{Path.Combine(modelsDir, "RealESRGAN")}\n"
                + $"{Path.Combine(modelsDir, "SwinIR")}";
            nodeValue.Children["embeddings"] = Path.Combine(modelsDir, "TextualInversion");
            nodeValue.Children["hypernetworks"] = Path.Combine(modelsDir, "Hypernetwork");
            nodeValue.Children["controlnet"] = string.Join(
                '\n',
                Path.Combine(modelsDir, "ControlNet"),
                Path.Combine(modelsDir, "T2IAdapter")
            );
            nodeValue.Children["clip"] = Path.Combine(modelsDir, "CLIP");
            nodeValue.Children["clip_vision"] = Path.Combine(modelsDir, "InvokeClipVision");
            nodeValue.Children["diffusers"] = Path.Combine(modelsDir, "Diffusers");
            nodeValue.Children["gligen"] = Path.Combine(modelsDir, "GLIGEN");
            nodeValue.Children["vae_approx"] = Path.Combine(modelsDir, "ApproxVAE");
            nodeValue.Children["ipadapter"] = string.Join(
                '\n',
                Path.Combine(modelsDir, "IpAdapter"),
                Path.Combine(modelsDir, "InvokeIpAdapters15"),
                Path.Combine(modelsDir, "InvokeIpAdaptersXl")
            );
            nodeValue.Children["prompt_expansion"] = Path.Combine(modelsDir, "PromptExpansion");
            nodeValue.Children["ultralytics"] = Path.Combine(modelsDir, "Ultralytics");
            nodeValue.Children["ultralytics_bbox"] = Path.Combine(modelsDir, "Ultralytics", "bbox");
            nodeValue.Children["ultralytics_segm"] = Path.Combine(modelsDir, "Ultralytics", "segm");
            nodeValue.Children["sams"] = Path.Combine(modelsDir, "Sams");
            nodeValue.Children["unet"] = Path.Combine(modelsDir, "unet");
        }
        else
        {
            stabilityMatrixNode = new KeyValuePair<YamlNode, YamlNode>(
                new YamlScalarNode("stability_matrix"),
                new YamlMappingNode
                {
                    { "checkpoints", Path.Combine(modelsDir, "StableDiffusion") },
                    { "vae", Path.Combine(modelsDir, "VAE") },
                    { "loras", $"{Path.Combine(modelsDir, "Lora")}\n{Path.Combine(modelsDir, "LyCORIS")}" },
                    {
                        "upscale_models",
                        $"{Path.Combine(modelsDir, "ESRGAN")}\n{Path.Combine(modelsDir, "RealESRGAN")}\n{Path.Combine(modelsDir, "SwinIR")}"
                    },
                    { "embeddings", Path.Combine(modelsDir, "TextualInversion") },
                    { "hypernetworks", Path.Combine(modelsDir, "Hypernetwork") },
                    {
                        "controlnet",
                        string.Join(
                            '\n',
                            Path.Combine(modelsDir, "ControlNet"),
                            Path.Combine(modelsDir, "T2IAdapter")
                        )
                    },
                    { "clip", Path.Combine(modelsDir, "CLIP") },
                    { "clip_vision", Path.Combine(modelsDir, "InvokeClipVision") },
                    { "diffusers", Path.Combine(modelsDir, "Diffusers") },
                    { "gligen", Path.Combine(modelsDir, "GLIGEN") },
                    { "vae_approx", Path.Combine(modelsDir, "ApproxVAE") },
                    {
                        "ipadapter",
                        string.Join(
                            '\n',
                            Path.Combine(modelsDir, "IpAdapter"),
                            Path.Combine(modelsDir, "InvokeIpAdapters15"),
                            Path.Combine(modelsDir, "InvokeIpAdaptersXl")
                        )
                    },
                    { "prompt_expansion", Path.Combine(modelsDir, "PromptExpansion") },
                    { "ultralytics", Path.Combine(modelsDir, "Ultralytics") },
                    { "ultralytics_bbox", Path.Combine(modelsDir, "Ultralytics", "bbox") },
                    { "ultralytics_segm", Path.Combine(modelsDir, "Ultralytics", "segm") },
                    { "sams", Path.Combine(modelsDir, "Sams") },
                    { "unet", Path.Combine(modelsDir, "unet") }
                }
            );
        }

        var newRootNode = new YamlMappingNode();
        foreach (var child in mappingNode.Children.Where(c => c.Key.ToString() != "stability_matrix"))
        {
            newRootNode.Children.Add(child);
        }

        newRootNode.Children.Add(stabilityMatrixNode);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithDefaultScalarStyle(ScalarStyle.Literal)
            .Build();

        var yamlData = serializer.Serialize(newRootNode);
        await extraPathsYamlPath.WriteAllTextAsync(yamlData).ConfigureAwait(false);
    }

    private static async Task RemoveConfigSection(DirectoryPath installDirectory)
    {
        var extraPathsYamlPath = installDirectory.JoinFile("extra_model_paths.yaml");

        if (!extraPathsYamlPath.Exists)
        {
            return;
        }

        var yaml = await extraPathsYamlPath.ReadAllTextAsync().ConfigureAwait(false);
        using var sr = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(sr);

        if (!yamlStream.Documents.Any())
        {
            return;
        }

        var root = yamlStream.Documents[0].RootNode;
        if (root is not YamlMappingNode mappingNode)
        {
            return;
        }

        mappingNode.Children.Remove("stability_matrix");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yamlData = serializer.Serialize(mappingNode);

        await extraPathsYamlPath.WriteAllTextAsync(yamlData).ConfigureAwait(false);
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
                "https://cdn.jsdelivr.net/gh/LykosAI/ComfyUI-Extensions-Index/custom-node-list.json"
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

                return jsonManifest?.GetPackageExtensions() ?? Enumerable.Empty<PackageExtension>();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get package extensions");
                return Enumerable.Empty<PackageExtension>();
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

            await PostInstallAsync(installedPackage, installedDirs, progress, cancellationToken)
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

            await PostInstallAsync(installedPackage, installedDirs, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Runs post install / update tasks (i.e. install.py, requirements.txt)
        /// </summary>
        private async Task PostInstallAsync(
            InstalledPackage installedPackage,
            IEnumerable<DirectoryPath> installedDirs,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
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
                            .SetupVenvPure(installedPackage.FullPath!)
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
                        .SetupVenvPure(installedPackage.FullPath!)
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
}
