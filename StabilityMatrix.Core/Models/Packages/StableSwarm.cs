using System.Diagnostics;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticDataSyntax;
using Injectio.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FDS;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, StableSwarm>(Duplicate = DuplicateStrategy.Append)]
public class StableSwarm(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    private Process? dotnetProcess;

    public override string Name => "StableSwarmUI";
    public override string RepositoryName => "SwarmUI";
    public override string DisplayName { get; set; } = "SwarmUI";
    public override string Author => "mcmonkeyprojects";
    public override string Blurb =>
        "A Modular Stable Diffusion Web-User-Interface, with an emphasis on making powertools easily accessible, high performance, and extensibility.";

    public override string LicenseType => "MIT";
    public override string LicenseUrl =>
        "https://github.com/mcmonkeyprojects/SwarmUI/blob/master/LICENSE.txt";
    public override string LaunchCommand => string.Empty;
    public override Uri PreviewImageUri =>
        new("https://github.com/mcmonkeyprojects/SwarmUI/raw/master/.github/images/swarmui.jpg");
    public override string OutputFolderName => "Output";
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.Symlink, SharedFolderMethod.Configuration, SharedFolderMethod.None];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override bool OfferInOneClickInstaller => false;
    public override bool UsesVenv => false;

    public override List<ExtraPackageCommand> GetExtraCommands() =>
        [
            new()
            {
                CommandName = "Rebuild .NET Project",
                Command = async installedPackage =>
                {
                    if (installedPackage == null || string.IsNullOrEmpty(installedPackage.FullPath))
                    {
                        throw new InvalidOperationException("Package not found or not installed correctly");
                    }

                    var srcFolder = Path.Combine(installedPackage.FullPath, "src");
                    var csprojName = "StableSwarmUI.csproj";
                    if (File.Exists(Path.Combine(srcFolder, "SwarmUI.csproj")))
                    {
                        csprojName = "SwarmUI.csproj";
                    }

                    await RebuildDotnetProject(installedPackage.FullPath, csprojName, null)
                        .ConfigureAwait(false);
                },
            },
        ];

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new LaunchOptionDefinition
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--host"],
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7801",
                Options = ["--port"],
            },
            new LaunchOptionDefinition
            {
                Name = "Ngrok Path",
                Type = LaunchOptionType.String,
                Options = ["--ngrok-path"],
            },
            new LaunchOptionDefinition
            {
                Name = "Ngrok Basic Auth",
                Type = LaunchOptionType.String,
                Options = ["--ngrok-basic-auth"],
            },
            new LaunchOptionDefinition
            {
                Name = "Cloudflared Path",
                Type = LaunchOptionType.String,
                Options = ["--cloudflared-path"],
            },
            new LaunchOptionDefinition
            {
                Name = "Proxy Region",
                Type = LaunchOptionType.String,
                Options = ["--proxy-region"],
            },
            new LaunchOptionDefinition
            {
                Name = "Launch Mode",
                Type = LaunchOptionType.Bool,
                Options = ["--launch-mode web", "--launch-mode webinstall"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = Path.Combine("Data/Settings.fds"),
            ConfigFileType = ConfigFileType.Fds,
            Rules =
            [
                new SharedFolderLayoutRule { IsRoot = true, ConfigDocumentPaths = ["ModelRoot"] },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["Models/Stable-Diffusion"],
                    ConfigDocumentPaths = ["SDModelFolder"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["Models/Lora"],
                    ConfigDocumentPaths = ["SDLoraFolder"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["Models/VAE"],
                    ConfigDocumentPaths = ["SDVAEFolder"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Embeddings],
                    TargetRelativePaths = ["Models/Embeddings"],
                    ConfigDocumentPaths = ["SDEmbeddingFolder"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ControlNet, SharedFolderType.T2IAdapter],
                    TargetRelativePaths = ["Models/controlnet"],
                    ConfigDocumentPaths = ["SDControlNetsFolder"],
                }, // Assuming Swarm maps T2I to ControlNet folder
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ClipVision],
                    TargetRelativePaths = ["Models/clip_vision"],
                    ConfigDocumentPaths = ["SDClipVisionFolder"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["Models/clip"],
                    ConfigDocumentPaths = ["SDClipFolder"],
                },
            ],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = [OutputFolderName] };
    public override string MainBranch => "master";
    public override bool ShouldIgnoreReleases => true;
    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps];
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        [
            PackagePrerequisite.Git,
            PackagePrerequisite.Dotnet,
            PackagePrerequisite.PythonUvManaged,
            PackagePrerequisite.VcRedist,
        ];

    private FilePath GetSettingsPath(string installLocation) =>
        Path.Combine(installLocation, "Data", "Settings.fds");

    private FilePath GetBackendsPath(string installLocation) =>
        Path.Combine(installLocation, "Data", "Backends.fds");

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing SwarmUI...", isIndeterminate: true));

        var comfy = settingsManager.Settings.InstalledPackages.FirstOrDefault(x =>
            x.PackageName is nameof(ComfyUI) or "ComfyUI-Zluda"
        );

        if (comfy == null)
        {
            throw new InvalidOperationException("ComfyUI must be installed to use SwarmUI");
        }

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
                        "\"NuGet official package source\"",
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

        var srcFolder = Path.Combine(installLocation, "src");
        var csprojName = "StableSwarmUI.csproj";
        if (File.Exists(Path.Combine(srcFolder, "SwarmUI.csproj")))
        {
            csprojName = "SwarmUI.csproj";
        }

        await prerequisiteHelper
            .RunDotnet(
                ["build", $"src/{csprojName}", "--configuration", "Release", "-o", "src/bin/live_release"],
                workingDirectory: installLocation,
                onProcessOutput: onConsoleOutput
            )
            .ConfigureAwait(false);

        if (!options.IsUpdate)
        {
            // set default settings
            var settings = new StableSwarmSettings { IsInstalled = true };

            if (options.SharedFolderMethod is SharedFolderMethod.Configuration)
            {
                settings.Paths = new StableSwarmSettings.PathsData
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
                    SDVAEFolder = Path.Combine(
                        settingsManager.ModelsDirectory,
                        SharedFolderType.VAE.ToString()
                    ),
                    SDEmbeddingFolder = Path.Combine(
                        settingsManager.ModelsDirectory,
                        SharedFolderType.Embeddings.ToString()
                    ),
                    SDControlNetsFolder = Path.Combine(
                        settingsManager.ModelsDirectory,
                        SharedFolderType.ControlNet.ToString()
                    ),
                    SDClipVisionFolder = Path.Combine(
                        settingsManager.ModelsDirectory,
                        SharedFolderType.ClipVision.ToString()
                    ),
                };
            }

            settings.Save(true).SaveToFile(GetSettingsPath(installLocation));

            var backendsFile = new FDSSection();
            var dataSection = new FDSSection();
            dataSection.Set("type", "comfyui_selfstart");
            dataSection.Set("title", "StabilityMatrix ComfyUI Self-Start");
            dataSection.Set("enabled", true);

            var launchArgs = comfy.LaunchArgs ?? [];
            var comfyArgs = string.Join(
                ' ',
                launchArgs
                    .Select(arg => arg.ToArgString()?.TrimEnd())
                    .Where(arg => !string.IsNullOrWhiteSpace(arg))
            );

            if (comfy.PackageName == "ComfyUI-Zluda")
            {
                var fullComfyZludaPath = Path.Combine(SettingsManager.LibraryDir, comfy.LibraryPath);
                var zludaPath = Path.Combine(fullComfyZludaPath, "zluda", "zluda.exe");
                var comfyVenvPath = Path.Combine(
                    fullComfyZludaPath,
                    "venv",
                    Compat.Switch(
                        (PlatformKind.Windows, Path.Combine("Scripts", "python.exe")),
                        (PlatformKind.Unix, Path.Combine("bin", "python3"))
                    )
                );

                ProcessArgs args = ["--", comfyVenvPath, "main.py", comfyArgs];

                // Create a wrapper batch file that runs zluda.exe
                var wrapperScriptPath = Path.Combine(installLocation, "Data", "zluda_wrapper.bat");
                var scriptContent = $"""
                    @echo off
                    "{zludaPath}" {args}
                    """;

                // Ensure the Data directory exists
                Directory.CreateDirectory(Path.Combine(installLocation, "Data"));

                // Write the batch file
                await File.WriteAllTextAsync(wrapperScriptPath, scriptContent, cancellationToken)
                    .ConfigureAwait(false);

                dataSection.Set(
                    "settings",
                    new ComfyUiSelfStartSettings
                    {
                        StartScript = wrapperScriptPath,
                        DisableInternalArgs = false,
                        AutoUpdate = false,
                        UpdateManagedNodes = "true",
                        ExtraArgs = string.Empty, // Arguments are already in the batch file
                    }.Save(true)
                );
            }
            else
            {
                dataSection.Set(
                    "settings",
                    new ComfyUiSelfStartSettings
                    {
                        StartScript = $"../{comfy.DisplayName}/main.py",
                        DisableInternalArgs = false,
                        AutoUpdate = false,
                        UpdateManagedNodes = "true",
                        ExtraArgs = comfyArgs,
                    }.Save(true)
                );
            }

            backendsFile.Set("0", dataSection);
            backendsFile.SaveToFile(GetBackendsPath(installLocation));
        }
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var portableGitBin = new DirectoryPath(PrerequisiteHelper.GitBinPath);
        var dotnetDir = PrerequisiteHelper.DotnetDir;
        var aspEnvVars = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_URLS"] = "http://*:7801",
            ["GIT"] = portableGitBin.JoinFile("git.exe"),
            ["DOTNET_ROOT"] = dotnetDir.FullPath,
        };

        if (aspEnvVars.TryGetValue("PATH", out var pathValue))
        {
            aspEnvVars["PATH"] = Compat.GetEnvPathWithExtensions(
                dotnetDir.FullPath,
                portableGitBin,
                pathValue
            );
        }
        else
        {
            aspEnvVars["PATH"] = Compat.GetEnvPathWithExtensions(dotnetDir.FullPath, portableGitBin);
        }

        aspEnvVars.Update(settingsManager.Settings.EnvironmentVariables);

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

        var sharedDiffusionModelsPath = new DirectoryPath(
            settingsManager.ModelsDirectory,
            nameof(SharedFolderType.DiffusionModels)
        );
        var swarmDiffusionModelsPath = new DirectoryPath(settingsManager.ModelsDirectory, "diffusion_models");

        try
        {
            swarmDiffusionModelsPath.Create();
            await Helper
                .SharedFolders.CreateOrUpdateLink(sharedDiffusionModelsPath, swarmDiffusionModelsPath)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            onConsoleOutput?.Invoke(
                new ProcessOutput
                {
                    Text =
                        $"Failed to create symlink for {nameof(SharedFolderType.DiffusionModels)}: {e.Message}.",
                }
            );
        }

        var launchScriptPath = Path.Combine(
            installLocation,
            Compat.IsWindows ? "launch-windows.bat"
                : Compat.IsMacOS ? "launch-macos.sh"
                : "launch-linux.sh"
        );

        dotnetProcess = ProcessRunner.StartAnsiProcess(
            launchScriptPath,
            options.Arguments,
            installLocation,
            HandleConsoleOutput,
            aspEnvVars
        );
    }

    public override async Task<bool> CheckForUpdates(InstalledPackage package)
    {
        var needsMigrate = false;
        try
        {
            var output = await prerequisiteHelper
                .GetGitOutput(["remote", "get-url", "origin"], package.FullPath)
                .ConfigureAwait(false);

            if (
                output.StandardOutput != null
                && output.StandardOutput.Contains("Stability", StringComparison.OrdinalIgnoreCase)
            )
            {
                needsMigrate = true;
            }
        }
        catch (Exception)
        {
            needsMigrate = true;
        }

        if (needsMigrate)
        {
            await prerequisiteHelper
                .RunGit(["remote", "set-url", "origin", GithubUrl], workingDirectory: package.FullPath)
                .ConfigureAwait(false);
        }

        return await base.CheckForUpdates(package).ConfigureAwait(false);
    }

    /*public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink
                => base.SetupModelFolders(installDirectory, SharedFolderMethod.Symlink),
            SharedFolderMethod.Configuration => SetupModelFoldersConfig(installDirectory),
            _ => Task.CompletedTask
        };

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink => base.RemoveModelFolderLinks(installDirectory, sharedFolderMethod),
            SharedFolderMethod.Configuration => RemoveModelFoldersConfig(installDirectory),
            _ => Task.CompletedTask
        };*/

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

    public async Task RebuildDotnetProject(
        string installLocation,
        string csprojName,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        await prerequisiteHelper
            .RunDotnet(
                [
                    "build",
                    $"src/{csprojName}",
                    "--no-incremental",
                    "--configuration",
                    "Release",
                    "-o",
                    "src/bin/live_release",
                ],
                workingDirectory: installLocation,
                onProcessOutput: onConsoleOutput
            )
            .ConfigureAwait(false);
    }

    private Task SetupModelFoldersConfig(DirectoryPath installDirectory)
    {
        var settingsPath = GetSettingsPath(installDirectory);
        var existingSettings = new StableSwarmSettings();
        var settingsExists = File.Exists(settingsPath);
        if (settingsExists)
        {
            var section = FDSUtility.ReadFile(settingsPath);
            var paths = section.GetSection("Paths");
            paths.Set("ModelRoot", settingsManager.ModelsDirectory);
            paths.Set(
                "SDModelFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.StableDiffusion.ToString())
            );
            paths.Set(
                "SDLoraFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.Lora.ToString())
            );
            paths.Set(
                "SDVAEFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.VAE.ToString())
            );
            paths.Set(
                "SDEmbeddingFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.Embeddings.ToString())
            );
            paths.Set(
                "SDControlNetsFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.ControlNet.ToString())
            );
            paths.Set(
                "SDClipVisionFolder",
                Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.ClipVision.ToString())
            );
            section.Set("Paths", paths);
            section.SaveToFile(settingsPath);
            return Task.CompletedTask;
        }

        existingSettings.Paths = new StableSwarmSettings.PathsData
        {
            ModelRoot = settingsManager.ModelsDirectory,
            SDModelFolder = Path.Combine(
                settingsManager.ModelsDirectory,
                SharedFolderType.StableDiffusion.ToString()
            ),
            SDLoraFolder = Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.Lora.ToString()),
            SDVAEFolder = Path.Combine(settingsManager.ModelsDirectory, SharedFolderType.VAE.ToString()),
            SDEmbeddingFolder = Path.Combine(
                settingsManager.ModelsDirectory,
                SharedFolderType.Embeddings.ToString()
            ),
            SDControlNetsFolder = Path.Combine(
                settingsManager.ModelsDirectory,
                SharedFolderType.ControlNet.ToString()
            ),
            SDClipVisionFolder = Path.Combine(
                settingsManager.ModelsDirectory,
                SharedFolderType.ClipVision.ToString()
            ),
        };

        existingSettings.Save(true).SaveToFile(settingsPath);

        return Task.CompletedTask;
    }

    private Task RemoveModelFoldersConfig(DirectoryPath installDirectory)
    {
        var settingsPath = GetSettingsPath(installDirectory);
        var existingSettings = new StableSwarmSettings();
        var settingsExists = File.Exists(settingsPath);
        if (settingsExists)
        {
            var section = FDSUtility.ReadFile(settingsPath);
            existingSettings.Load(section);
        }

        existingSettings.Paths = new StableSwarmSettings.PathsData();
        existingSettings.Save(true).SaveToFile(settingsPath);

        return Task.CompletedTask;
    }
}
