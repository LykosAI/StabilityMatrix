using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticDataSyntax;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FDS;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class StableSwarm(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private Process? dotnetProcess;

    public override string Name => "StableSwarmUI";
    public override string DisplayName { get; set; } = "StableSwarmUI";
    public override string Author => "Stability-AI";

    public override string Blurb =>
        "A Modular Stable Diffusion Web-User-Interface, with an emphasis on making powertools easily accessible, high performance, and extensibility.";

    public override string LicenseType => "MIT";
    public override string LicenseUrl =>
        "https://github.com/Stability-AI/StableSwarmUI/blob/master/LICENSE.txt";
    public override string LaunchCommand => string.Empty;
    public override Uri PreviewImageUri =>
        new(
            "https://raw.githubusercontent.com/Stability-AI/StableSwarmUI/master/.github/images/stableswarmui.jpg"
        );
    public override string OutputFolderName => "Output";
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.Configuration, SharedFolderMethod.None];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];
    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }
    public override string MainBranch => "master";
    public override bool ShouldIgnoreReleases => true;
    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        [TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.DirectMl, TorchVersion.Rocm, TorchVersion.Mps];
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        [
            PackagePrerequisite.Git,
            PackagePrerequisite.Dotnet,
            PackagePrerequisite.Python310,
            PackagePrerequisite.VcRedist
        ];

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing StableSwarmUI...", isIndeterminate: true));

        try
        {
            await prerequisiteHelper
                .RunDotnet(
                    [
                        "nuget",
                        "add",
                        "source",
                        "https://api.nuget.org/v3/index.json",
                        "--name",
                        "\"NuGet official package source\""
                    ],
                    workingDirectory: installLocation,
                    onProcessOutput: onConsoleOutput
                )
                .ConfigureAwait(false);
        }
        catch (ProcessException e)
        {
            // ignore, probably means the source is already there
        }

        await prerequisiteHelper
            .RunDotnet(
                [
                    "build",
                    "src/StableSwarmUI.csproj",
                    "--configuration",
                    "Release",
                    "-o",
                    "src/bin/live_release"
                ],
                workingDirectory: installLocation,
                onProcessOutput: onConsoleOutput
            )
            .ConfigureAwait(false);

        // set default settings
        var settings = new StableSwarmSettings
        {
            IsInstalled = true,
            Paths = new StableSwarmSettings.PathsData
            {
                ModelRoot = settingsManager.ModelsDirectory,
                SDModelFolder = Path.Combine(
                    settingsManager.ModelsDirectory,
                    SharedFolderType.StableDiffusion.ToString()
                ),
                SDLoraFolder = Path.Combine(
                    settingsManager.ModelsDirectory,
                    SharedFolderType.Lora.ToString()
                ),
                SDVAEFolder = Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.VAE.ToString()),
                SDEmbeddingFolder = Path.Combine(
                    settingsManager.ModelsDirectory,
                    SharedFolderType.TextualInversion.ToString()
                ),
                SDControlNetsFolder = Path.Combine(
                    settingsManager.ModelsDirectory,
                    SharedFolderType.ControlNet.ToString()
                ),
                SDClipVisionFolder = Path.Combine(
                    settingsManager.ModelsDirectory,
                    SharedFolderType.InvokeClipVision.ToString()
                )
            }
        };

        settings.Save(true).SaveToFile(Path.Combine(installLocation, "Data", "Settings.fds"));

        var backendsFile = new FDSSection();
        var comfy = settingsManager.Settings.InstalledPackages.FirstOrDefault(
            x => x.PackageName == nameof(ComfyUI)
        );

        if (comfy == null)
        {
            throw new InvalidOperationException("ComfyUI must be installed to use StableSwarmUI");
        }

        var dataSection = new FDSSection();
        dataSection.Set("type", "comfyui_selfstart");
        dataSection.Set("title", "StabilityMatrix ComfyUI Self-Start");
        dataSection.Set("enabled", true);
        dataSection.Set(
            "settings",
            new ComfyUiSelfStartSettings
            {
                StartScript = $"../{comfy.DisplayName}/main.py",
                DisableInternalArgs = false,
                AutoUpdate = false,
                // TODO: ??? ExtraLaunchArguments = comfy.LaunchArgs.Select(x => x.???)
            }.Save(true)
        );

        backendsFile.Set("0", dataSection);
        backendsFile.SaveToFile(Path.Combine(installLocation, "Data", "Backends.fds"));
    }

    public override async Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        var aspEnvVars = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_URLS"] = "http://*:7801",
            ["DOTNET_ROOT(x86)"] = Path.Combine(settingsManager.LibraryDir, "Assets", "dotnet8")
        };

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (s.Text.Contains("Starting webserver", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
            }
        }

        dotnetProcess = await prerequisiteHelper
            .RunDotnet(
                ["src\\bin\\live_release\\StableSwarmUI.dll"],
                workingDirectory: installedPackagePath,
                envVars: aspEnvVars,
                onProcessOutput: HandleConsoleOutput,
                waitForExit: false
            )
            .ConfigureAwait(false);
    }

    public override async Task WaitForShutdown()
    {
        if (dotnetProcess is { HasExited: false })
        {
            dotnetProcess.Kill(true);
            try
            {
                await dotnetProcess
                    .WaitForExitAsync(new CancellationTokenSource(5000).Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine(e);
            }
        }

        dotnetProcess = null;
        GC.SuppressFinalize(this);
    }
}
