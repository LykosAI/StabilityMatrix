using System.Diagnostics;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class ComfyUI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper)
    : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/comfyanonymous/ComfyUI/blob/master/LICENSE";
    public override string Blurb => "A powerful and modular stable diffusion GUI and backend";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/comfyanonymous/ComfyUI/raw/master/comfyui_screenshot.png");
    public override bool ShouldIgnoreReleases => true;
    public override bool IsInferenceCompatible => true;
    public override string OutputFolderName => "output";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;

    public override SharedFolderMethod RecommendedSharedFolderMethod =>
        SharedFolderMethod.Configuration;

    // https://github.com/comfyanonymous/ComfyUI/blob/master/folder_paths.py#L11
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/checkpoints" },
            [SharedFolderType.Diffusers] = new[] { "models/diffusers" },
            [SharedFolderType.Lora] = new[] { "models/loras" },
            [SharedFolderType.CLIP] = new[] { "models/clip" },
            [SharedFolderType.TextualInversion] = new[] { "models/embeddings" },
            [SharedFolderType.VAE] = new[] { "models/vae" },
            [SharedFolderType.ApproxVAE] = new[] { "models/vae_approx" },
            [SharedFolderType.ControlNet] = new[] { "models/controlnet" },
            [SharedFolderType.GLIGEN] = new[] { "models/gligen" },
            [SharedFolderType.ESRGAN] = new[] { "models/upscale_models" },
            [SharedFolderType.Hypernetwork] = new[] { "models/hypernetworks" },
            [SharedFolderType.IpAdapter] = new[] { "models/ipadapter" },
            [SharedFolderType.T2IAdapter] = new[] { "models/controlnet" },
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "output" } };

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
            InitialValue = HardwareHelper
                    .IterGpuInfo()
                    .Select(gpu => gpu.MemoryLevel)
                    .Max() switch
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
            Options =
            [
                "--preview-method auto",
                "--preview-method latent2rgb",
                "--preview-method taesd"
            ]
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
            InitialValue = !HardwareHelper.HasNvidiaGpu() && !HardwareHelper.HasAmdGpu(),
            Options = ["--cpu"]
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
        new[]
        {
            TorchVersion.Cpu,
            TorchVersion.Cuda,
            TorchVersion.DirectMl,
            TorchVersion.Rocm,
            TorchVersion.Mps
        };

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);

        // Install torch / xformers based on gpu info
        switch (torchVersion)
        {
            case TorchVersion.Cpu:
                await InstallCpuTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                break;
            case TorchVersion.Cuda:
                await venvRunner
                    .PipInstall(
                        new PipInstallArgs()
                            .WithTorch("~=2.1.0")
                            .WithTorchVision()
                            .WithXFormers("==0.0.22.post4")
                            .AddArg("--upgrade")
                            .WithTorchExtraIndex("cu121"),
                        onConsoleOutput
                    )
                    .ConfigureAwait(false);
                break;
            case TorchVersion.DirectMl:
                await venvRunner
                    .PipInstall(new PipInstallArgs().WithTorchDirectML(), onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.Rocm:
                await InstallRocmTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                break;
            case TorchVersion.Mps:
                await venvRunner
                    .PipInstall(
                        new PipInstallArgs()
                            .AddArg("--pre")
                            .WithTorch()
                            .WithTorchVision()
                            .WithTorchExtraIndex("nightly/cpu"),
                        onConsoleOutput
                    )
                    .ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null);
        }

        // Install requirements file (skip torch)
        progress?.Report(
            new ProgressReport(-1, "Installing Package Requirements", isIndeterminate: true)
        );

        var requirementsFile = new FilePath(installLocation, "requirements.txt");

        await venvRunner
            .PipInstallFromRequirements(requirementsFile, onConsoleOutput, excludes: "torch")
            .ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(1, "Installing Package Requirements", isIndeterminate: false)
        );
    }

    private async Task AutoDetectAndInstallTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null
    )
    {
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        if (gpus.Any(g => g.IsNvidia))
        {
            await InstallCudaTorch(venvRunner, progress).ConfigureAwait(false);
        }
        else if (HardwareHelper.PreferRocm())
        {
            await InstallRocmTorch(venvRunner, progress).ConfigureAwait(false);
        }
        else if (HardwareHelper.PreferDirectML())
        {
            await InstallDirectMlTorch(venvRunner, progress).ConfigureAwait(false);
        }
        else
        {
            await InstallCpuTorch(venvRunner, progress).ConfigureAwait(false);
        }
    }

    public override async Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);
        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner?.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit);
        return;

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

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
    )
    {
        switch (sharedFolderMethod)
        {
            case SharedFolderMethod.None:
                return Task.CompletedTask;
            case SharedFolderMethod.Symlink:
                return base.SetupModelFolders(installDirectory, sharedFolderMethod);
        }

        var extraPathsYamlPath = installDirectory + "extra_model_paths.yaml";
        var modelsDir = SettingsManager.ModelsDirectory;

        var exists = File.Exists(extraPathsYamlPath);
        if (!exists)
        {
            Logger.Info("Creating extra_model_paths.yaml");
            File.WriteAllText(extraPathsYamlPath, string.Empty);
        }
        var yaml = File.ReadAllText(extraPathsYamlPath);
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
                return Task.CompletedTask;

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
            nodeValue.Children["controlnet"] = Path.Combine(modelsDir, "ControlNet");
            nodeValue.Children["clip"] = Path.Combine(modelsDir, "CLIP");
            nodeValue.Children["diffusers"] = Path.Combine(modelsDir, "Diffusers");
            nodeValue.Children["gligen"] = Path.Combine(modelsDir, "GLIGEN");
            nodeValue.Children["vae_approx"] = Path.Combine(modelsDir, "ApproxVAE");
        }
        else
        {
            stabilityMatrixNode = new KeyValuePair<YamlNode, YamlNode>(
                new YamlScalarNode("stability_matrix"),
                new YamlMappingNode
                {
                    { "checkpoints", Path.Combine(modelsDir, "StableDiffusion") },
                    { "vae", Path.Combine(modelsDir, "VAE") },
                    {
                        "loras",
                        $"{Path.Combine(modelsDir, "Lora")}\n{Path.Combine(modelsDir, "LyCORIS")}"
                    },
                    {
                        "upscale_models",
                        $"{Path.Combine(modelsDir, "ESRGAN")}\n{Path.Combine(modelsDir, "RealESRGAN")}\n{Path.Combine(modelsDir, "SwinIR")}"
                    },
                    { "embeddings", Path.Combine(modelsDir, "TextualInversion") },
                    { "hypernetworks", Path.Combine(modelsDir, "Hypernetwork") },
                    { "controlnet", Path.Combine(modelsDir, "ControlNet") },
                    { "clip", Path.Combine(modelsDir, "CLIP") },
                    { "diffusers", Path.Combine(modelsDir, "Diffusers") },
                    { "gligen", Path.Combine(modelsDir, "GLIGEN") },
                    { "vae_approx", Path.Combine(modelsDir, "ApproxVAE") }
                }
            );
        }

        var newRootNode = new YamlMappingNode();
        foreach (
            var child in mappingNode.Children.Where(c => c.Key.ToString() != "stability_matrix")
        )
        {
            newRootNode.Children.Add(child);
        }

        newRootNode.Children.Add(stabilityMatrixNode);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yamlData = serializer.Serialize(newRootNode);
        File.WriteAllText(extraPathsYamlPath, yamlData);

        return Task.CompletedTask;
    }

    public override Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink
                => base.UpdateModelFolders(installDirectory, sharedFolderMethod),
            SharedFolderMethod.Configuration
                => SetupModelFolders(installDirectory, sharedFolderMethod),
            SharedFolderMethod.None => Task.CompletedTask,
            _ => Task.CompletedTask
        };

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        return sharedFolderMethod switch
        {
            SharedFolderMethod.Configuration => RemoveConfigSection(installDirectory),
            SharedFolderMethod.None => Task.CompletedTask,
            SharedFolderMethod.Symlink
                => base.RemoveModelFolderLinks(installDirectory, sharedFolderMethod),
            _ => Task.CompletedTask
        };
    }

    private Task RemoveConfigSection(string installDirectory)
    {
        var extraPathsYamlPath = Path.Combine(installDirectory, "extra_model_paths.yaml");
        var exists = File.Exists(extraPathsYamlPath);
        if (!exists)
        {
            return Task.CompletedTask;
        }

        var yaml = File.ReadAllText(extraPathsYamlPath);
        using var sr = new StringReader(yaml);
        var yamlStream = new YamlStream();
        yamlStream.Load(sr);

        if (!yamlStream.Documents.Any())
        {
            return Task.CompletedTask;
        }

        var root = yamlStream.Documents[0].RootNode;
        if (root is not YamlMappingNode mappingNode)
        {
            return Task.CompletedTask;
        }

        mappingNode.Children.Remove("stability_matrix");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yamlData = serializer.Serialize(mappingNode);
        File.WriteAllText(extraPathsYamlPath, yamlData);

        return Task.CompletedTask;
    }

    private async Task InstallRocmTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(
            new ProgressReport(-1f, "Installing PyTorch for ROCm", isIndeterminate: true)
        );

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        await venvRunner
            .PipInstall(
                new PipInstallArgs()
                    .WithTorch("==2.0.1")
                    .WithTorchVision()
                    .WithTorchExtraIndex("rocm5.6"),
                onConsoleOutput
            )
            .ConfigureAwait(false);
    }

    public async Task SetupInferenceOutputFolderLinks(DirectoryPath installDirectory)
    {
        var inferenceDir = installDirectory.JoinDir("output", "Inference");

        var sharedInferenceDir = SettingsManager.ImagesInferenceDirectory;

        if (inferenceDir.IsSymbolicLink)
        {
            if (inferenceDir.Info.ResolveLinkTarget(true)?.FullName == sharedInferenceDir.FullPath)
            {
                // Already valid link, skip
                return;
            }

            // Otherwise delete so we don't have to move files
            await sharedInferenceDir.DeleteAsync(false).ConfigureAwait(false);
        }

        await Helper.SharedFolders
            .CreateOrUpdateLink(sharedInferenceDir, inferenceDir)
            .ConfigureAwait(false);
    }
}
