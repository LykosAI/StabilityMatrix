using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Sdfx>(Duplicate = DuplicateStrategy.Append)]
public class Sdfx(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "sdfx";
    public override string DisplayName { get; set; } = "SDFX";
    public override string Author => "sdfxai";
    public override string Blurb =>
        "The ultimate no-code platform to build and share AI apps with beautiful UI.";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/sdfxai/sdfx/blob/main/LICENSE";
    public override string LaunchCommand => "setup.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/sdfxai/sdfx/raw/main/docs/static/screen-sdfx.png");
    public override string OutputFolderName => Path.Combine("data", "media", "output");

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps];

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "data/models/checkpoints" },
            [SharedFolderType.Diffusers] = new[] { "data/models/diffusers" },
            [SharedFolderType.Lora] = new[] { "data/models/loras" },
            [SharedFolderType.CLIP] = new[] { "data/models/clip" },
            [SharedFolderType.InvokeClipVision] = new[] { "data/models/clip_vision" },
            [SharedFolderType.TextualInversion] = new[] { "data/models/embeddings" },
            [SharedFolderType.VAE] = new[] { "data/models/vae" },
            [SharedFolderType.ApproxVAE] = new[] { "data/models/vae_approx" },
            [SharedFolderType.ControlNet] = new[] { "data/models/controlnet/ControlNet" },
            [SharedFolderType.GLIGEN] = new[] { "data/models/gligen" },
            [SharedFolderType.ESRGAN] = new[] { "data/models/upscale_models" },
            [SharedFolderType.Hypernetwork] = new[] { "data/models/hypernetworks" },
            [SharedFolderType.IpAdapter] = new[] { "data/models/ipadapter/base" },
            [SharedFolderType.InvokeIpAdapters15] = new[] { "data/models/ipadapter/sd15" },
            [SharedFolderType.InvokeIpAdaptersXl] = new[] { "data/models/ipadapter/sdxl" },
            [SharedFolderType.T2IAdapter] = new[] { "data/models/controlnet/T2IAdapter" },
            [SharedFolderType.PromptExpansion] = new[] { "data/models/prompt_expansion" }
        };
    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "data/media/output" } };
    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        [
            PackagePrerequisite.Python310,
            PackagePrerequisite.VcRedist,
            PackagePrerequisite.Git,
            PackagePrerequisite.Node
        ];

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
        // Setup venv
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(GetEnvVars);

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        var gpuArg = torchVersion switch
        {
            TorchIndex.Cuda => "--nvidia",
            TorchIndex.Rocm => "--amd",
            TorchIndex.DirectMl => "--directml",
            TorchIndex.Cpu => "--cpu",
            TorchIndex.Mps => "--mac",
            _ => throw new NotSupportedException($"Torch version {torchVersion} is not supported.")
        };

        await venvRunner
            .CustomInstall(["setup.py", "--install", gpuArg], onConsoleOutput)
            .ConfigureAwait(false);

        if (installedPackage.PipOverrides != null)
        {
            var pipArgs = new PipInstallArgs().WithUserOverrides(installedPackage.PipOverrides);
            await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        }

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
        var venvRunner = await SetupVenv(
                installLocation,
                pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
            )
            .ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(GetEnvVars);

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;

            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }

        venvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), "--run", ..options.Arguments],
            HandleConsoleOutput,
            OnExit
        );

        // Cuz node was getting detached on process exit
        if (Compat.IsWindows)
        {
            ProcessTracker.AttachExitHandlerJobToProcess(venvRunner.Process);
        }
    }

    private ImmutableDictionary<string, string> GetEnvVars(ImmutableDictionary<string, string> env)
    {
        var pathBuilder = new EnvPathBuilder();

        if (env.TryGetValue("PATH", out var value))
        {
            pathBuilder.AddPath(value);
        }

        pathBuilder.AddPath(
            Compat.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                : Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs", "bin")
        );

        pathBuilder.AddPath(Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs"));

        return env.SetItem("PATH", pathBuilder.ToString());
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
        var configPath = Path.Combine(installDirectory, "sdfx.config.json");

        if (File.Exists(configPath))
        {
            var configText = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<JsonObject>(configText) ?? new JsonObject();
            var modelsDir = SettingsManager.ModelsDirectory;

            var models = config.GetOrAddNonNullJsonObject(["paths", "models"]);

            models["checkpoints"] = new JsonArray(Path.Combine(modelsDir, "StableDiffusion"));
            models["vae"] = new JsonArray(Path.Combine(modelsDir, "VAE"));
            models["loras"] = new JsonArray(
                Path.Combine(modelsDir, "Lora"),
                Path.Combine(modelsDir, "LyCORIS")
            );
            models["upscale_models"] = new JsonArray(
                Path.Combine(modelsDir, "ESRGAN"),
                Path.Combine(modelsDir, "RealESRGAN"),
                Path.Combine(modelsDir, "SwinIR")
            );
            models["embeddings"] = new JsonArray(Path.Combine(modelsDir, "TextualInversion"));
            models["hypernetworks"] = new JsonArray(Path.Combine(modelsDir, "Hypernetwork"));
            models["controlnet"] = new JsonArray(
                Path.Combine(modelsDir, "ControlNet"),
                Path.Combine(modelsDir, "T2IAdapter")
            );
            models["clip"] = new JsonArray(Path.Combine(modelsDir, "CLIP"));
            models["clip_vision"] = new JsonArray(Path.Combine(modelsDir, "InvokeClipVision"));
            models["diffusers"] = new JsonArray(Path.Combine(modelsDir, "Diffusers"));
            models["gligen"] = new JsonArray(Path.Combine(modelsDir, "GLIGEN"));
            models["vae_approx"] = new JsonArray(Path.Combine(modelsDir, "ApproxVAE"));
            models["ipadapter"] = new JsonArray(
                Path.Combine(modelsDir, "IpAdapter"),
                Path.Combine(modelsDir, "InvokeIpAdapters15"),
                Path.Combine(modelsDir, "InvokeIpAdaptersXl")
            );

            await File.WriteAllTextAsync(configPath, config.ToString()).ConfigureAwait(false);
        }
    }

    private async Task RemoveConfigSection(DirectoryPath installDirectory)
    {
        var configPath = Path.Combine(installDirectory, "sdfx.config.json");

        if (File.Exists(configPath))
        {
            var configText = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<JsonObject>(configText) ?? new JsonObject();

            var models = config.GetOrAddNonNullJsonObject(["paths", "models"]);

            models["checkpoints"] = new JsonArray(Path.Combine("data", "models", "checkpoints"));
            models["clip"] = new JsonArray(Path.Combine("data", "models", "clip"));
            models["clip_vision"] = new JsonArray(Path.Combine("data", "models", "clip_vision"));
            models["controlnet"] = new JsonArray(Path.Combine("data", "models", "controlnet"));
            models["diffusers"] = new JsonArray(Path.Combine("data", "models", "diffusers"));
            models["embeddings"] = new JsonArray(Path.Combine("data", "models", "embeddings"));
            models["gligen"] = new JsonArray(Path.Combine("data", "models", "gligen"));
            models["ipadapter"] = new JsonArray(Path.Combine("data", "models", "ipadapter"));
            models["hypernetworks"] = new JsonArray(Path.Combine("data", "models", "hypernetworks"));
            models["loras"] = new JsonArray(Path.Combine("data", "models", "loras"));
            models["upscale_models"] = new JsonArray(Path.Combine("data", "models", "upscale_models"));
            models["vae"] = new JsonArray(Path.Combine("data", "models", "vae"));
            models["vae_approx"] = new JsonArray(Path.Combine("data", "models", "vae_approx"));

            await File.WriteAllTextAsync(configPath, config.ToString()).ConfigureAwait(false);
        }
    }
}
