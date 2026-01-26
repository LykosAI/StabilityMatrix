using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Api.Invoke;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, InvokeAI>(Duplicate = DuplicateStrategy.Append)]
public class InvokeAI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
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
    private const string RelativeRootPath = "invokeai-root";
    private readonly string relativeFrontendBuildPath = Path.Combine("invokeai", "frontend", "web", "dist");

    public override string Name => "InvokeAI";
    public override string DisplayName { get; set; } = "InvokeAI";
    public override string Author => "invoke-ai";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/invoke-ai/InvokeAI/blob/main/LICENSE";

    public override string Blurb => "Professional Creative Tools for Stable Diffusion";
    public override string LaunchCommand => "invokeai-web";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;

    public override Uri PreviewImageUri =>
        new("https://raw.githubusercontent.com/invoke-ai/InvokeAI/main/docs/assets/canvas_preview.png");

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.None, SharedFolderMethod.Configuration];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;

    public override string MainBranch => "main";
    public override bool ShouldIgnoreBranches => true;

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = [Path.Combine(RelativeRootPath, "autoimport", "main")],
            [SharedFolderType.Lora] = [Path.Combine(RelativeRootPath, "autoimport", "lora")],
            [SharedFolderType.Embeddings] = [Path.Combine(RelativeRootPath, "autoimport", "embedding")],
            [SharedFolderType.ControlNet] = [Path.Combine(RelativeRootPath, "autoimport", "controlnet")],
            [SharedFolderType.IpAdapters15] =
            [
                Path.Combine(RelativeRootPath, "models", "sd-1", "ip_adapter"),
            ],
            [SharedFolderType.IpAdaptersXl] =
            [
                Path.Combine(RelativeRootPath, "models", "sdxl", "ip_adapter"),
            ],
            [SharedFolderType.ClipVision] = [Path.Combine(RelativeRootPath, "models", "any", "clip_vision")],
            [SharedFolderType.T2IAdapter] = [Path.Combine(RelativeRootPath, "autoimport", "t2i_adapter")],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = [Path.Combine("invokeai-root", "outputs", "images")] };

    public override string OutputFolderName => Path.Combine("invokeai-root", "outputs", "images");

    // https://github.com/invoke-ai/InvokeAI/blob/main/docs/features/CONFIGURATION.md
    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Root Directory",
                Type = LaunchOptionType.String,
                Options = ["--root"],
            },
            new()
            {
                Name = "Config File",
                Type = LaunchOptionType.String,
                Options = ["--config"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.Rocm, TorchIndex.Mps];

    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    public override TorchIndex GetRecommendedTorchVersion()
    {
        if (Compat.IsMacOS && Compat.IsArm)
        {
            return TorchIndex.Mps;
        }

        return base.GetRecommendedTorchVersion();
    }

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        [PackagePrerequisite.Python310, PackagePrerequisite.VcRedist, PackagePrerequisite.Git];

    public override Task DownloadPackage(
        string installLocation,
        DownloadPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.CompletedTask;
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
        // Backup existing files/folders except for known directories
        try
        {
            var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "invokeai-root",
                "invoke.old",
                "venv",
            };

            if (Directory.Exists(installLocation))
            {
                var entriesToMove = Directory
                    .EnumerateFileSystemEntries(installLocation)
                    .Where(p => !excludedNames.Contains(Path.GetFileName(p)))
                    .ToList();

                if (entriesToMove.Count > 0)
                {
                    var backupFolderName = "invoke.old";
                    var backupFolderPath = Path.Combine(installLocation, backupFolderName);

                    if (Directory.Exists(backupFolderPath) || File.Exists(backupFolderPath))
                    {
                        backupFolderPath = Path.Combine(
                            installLocation,
                            $"invoke.old.{DateTime.Now:yyyyMMddHHmmss}"
                        );
                    }

                    Directory.CreateDirectory(backupFolderPath);

                    foreach (var entry in entriesToMove)
                    {
                        var destinationPath = Path.Combine(backupFolderPath, Path.GetFileName(entry));

                        // Ensure we do not overwrite existing files if names collide
                        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                        {
                            var name = Path.GetFileNameWithoutExtension(entry);
                            var ext = Path.GetExtension(entry);
                            var uniqueName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                            destinationPath = Path.Combine(backupFolderPath, uniqueName);
                        }

                        if (Directory.Exists(entry))
                        {
                            Directory.Move(entry, destinationPath);
                        }
                        else if (File.Exists(entry))
                        {
                            File.Move(entry, destinationPath);
                        }
                    }

                    Logger.Info($"Moved {entriesToMove.Count} item(s) to '{backupFolderPath}'.");
                }
            }
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to move existing files to 'invoke.old'. Continuing with installation.");
        }

        // Setup venv
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(env => GetEnvVars(env, installLocation));

        progress?.Report(new ProgressReport(-1f, "Installing Package", isIndeterminate: true));

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var isLegacyNvidiaGpu =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();
        var fallbackIndex = torchVersion switch
        {
            TorchIndex.Cpu when Compat.IsLinux => "https://download.pytorch.org/whl/cpu",
            TorchIndex.Cuda when isLegacyNvidiaGpu => "https://download.pytorch.org/whl/cu126",
            TorchIndex.Cuda => "https://download.pytorch.org/whl/cu128",
            TorchIndex.Rocm => "https://download.pytorch.org/whl/rocm6.3",
            _ => string.Empty,
        };

        var invokeInstallArgs = new PipInstallArgs($"invokeai=={options.VersionOptions.VersionTag}");

        var contentStream = await DownloadService
            .GetContentAsync(
                $"https://raw.githubusercontent.com/invoke-ai/InvokeAI/refs/tags/{options.VersionOptions.VersionTag}/pins.json",
                cancellationToken
            )
            .ConfigureAwait(false);

        // read to json, just deserialize as JObject or whtaever it is in System.Text>json
        using var reader = new StreamReader(contentStream);
        var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var pins = JsonNode.Parse(json);
        var platform =
            Compat.IsWindows ? "win32"
            : Compat.IsMacOS ? "darwin"
            : "linux";
        var index = pins?["torchIndexUrl"]?[platform]?[
            torchVersion.ToString().ToLowerInvariant()
        ]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(index) && !isLegacyNvidiaGpu)
        {
            invokeInstallArgs = invokeInstallArgs.AddArg("--index").AddArg(index);
        }
        else if (!string.IsNullOrWhiteSpace(fallbackIndex))
        {
            invokeInstallArgs = invokeInstallArgs.AddArg("--index").AddArg(fallbackIndex);
        }

        invokeInstallArgs = invokeInstallArgs.AddArg("--force-reinstall");

        await venvRunner.PipInstall(invokeInstallArgs, onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Done!", isIndeterminate: false));
    }

    public override Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    ) =>
        RunInvokeCommand(
            installLocation,
            options.Command ?? LaunchCommand,
            options.Arguments,
            true,
            installedPackage,
            onConsoleOutput
        );

    private async Task RunInvokeCommand(
        string installedPackagePath,
        string command,
        string arguments,
        bool runDetached,
        InstalledPackage installedPackage,
        Action<ProcessOutput>? onConsoleOutput,
        bool spam3 = false
    )
    {
        if (spam3 && !runDetached)
        {
            throw new InvalidOperationException("Cannot spam 3 if not running detached");
        }

        await SetupVenv(installedPackagePath, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        VenvRunner.UpdateEnvironmentVariables(env => GetEnvVars(env, installedPackagePath));

        // Launch command is for a console entry point, and not a direct script
        var entryPoint = await VenvRunner.GetEntryPoint(command).ConfigureAwait(false);

        // Split at ':' to get package and function
        var split = entryPoint?.Split(':');

        // Console message because Invoke takes forever to start sometimes with no output of what its doing
        onConsoleOutput?.Invoke(new ProcessOutput { Text = "Starting InvokeAI...\n" });

        if (split is not { Length: > 1 })
        {
            throw new Exception($"Could not find entry point for InvokeAI: {entryPoint.ToRepr()}");
        }

        // Compile a startup command according to
        // https://packaging.python.org/en/latest/specifications/entry-points/#use-for-scripts
        // For invokeai, also patch the shutil.get_terminal_size function to return a fixed value
        // above the minimum in invokeai.frontend.install.widgets

        var code = $"""
            import sys
            from {split[0]} import {split[1]}
            sys.exit({split[1]}())
            """;

        if (runDetached)
        {
            async void HandleConsoleOutput(ProcessOutput s)
            {
                if (s.Text.Contains("running on", StringComparison.OrdinalIgnoreCase))
                {
                    var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                    var match = regex.Match(s.Text);
                    if (!match.Success)
                        return;

                    if (installedPackage.PreferredSharedFolderMethod == SharedFolderMethod.Configuration)
                    {
                        try
                        {
                            // returns true if we printed the url already cuz it took too long
                            if (
                                await SetupInvokeModelSharingConfig(onConsoleOutput, match, s)
                                    .ConfigureAwait(false)
                            )
                                return;
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Failed to setup InvokeAI model sharing config");
                        }
                    }

                    onConsoleOutput?.Invoke(s);

                    WebUrl = match.Value;
                    OnStartupComplete(WebUrl);
                }
                else
                {
                    onConsoleOutput?.Invoke(s);

                    if (
                        spam3
                        && s.Text.Contains("[3] Accept the best guess;", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        VenvRunner.Process?.StandardInput.WriteLine("3");
                    }
                }
            }

            VenvRunner.RunDetached($"-c \"{code}\" {arguments}".TrimEnd(), HandleConsoleOutput, OnExit);
        }
        else
        {
            var result = await VenvRunner.Run($"-c \"{code}\" {arguments}".TrimEnd()).ConfigureAwait(false);
            onConsoleOutput?.Invoke(new ProcessOutput { Text = result.StandardOutput });
        }
    }

    public override async Task<InstalledPackageVersion> Update(
        string installLocation,
        InstalledPackage installedPackage,
        UpdatePackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await InstallPackage(
                installLocation,
                installedPackage,
                options.AsInstallOptions(),
                progress,
                onConsoleOutput,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(options.VersionOptions.VersionTag))
        {
            return new InstalledPackageVersion
            {
                InstalledReleaseVersion = options.VersionOptions.VersionTag,
                IsPrerelease = options.VersionOptions.IsPrerelease,
            };
        }

        return new InstalledPackageVersion
        {
            InstalledBranch = options.VersionOptions.BranchName,
            InstalledCommitSha = options.VersionOptions.CommitHash,
            IsPrerelease = options.VersionOptions.IsPrerelease,
        };
    }

    // Invoke doing shared folders on startup instead
    public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) => Task.CompletedTask;

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) => Task.CompletedTask;

    private async Task<bool> SetupInvokeModelSharingConfig(
        Action<ProcessOutput>? onConsoleOutput,
        Match match,
        ProcessOutput s
    )
    {
        var invokeAiUrl = match.Value;
        if (invokeAiUrl.Contains("0.0.0.0"))
        {
            invokeAiUrl = invokeAiUrl.Replace("0.0.0.0", "127.0.0.1");
        }

        var invokeAiApi = RestService.For<IInvokeAiApi>(
            invokeAiUrl,
            new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                ),
            }
        );

        var result = await invokeAiApi.ScanFolder(SettingsManager.ModelsDirectory).ConfigureAwait(false);
        var modelsToScan = result.Where(x => !x.IsInstalled).ToList();
        if (modelsToScan.Count <= 0)
            return false;

        foreach (var model in modelsToScan)
        {
            Logger.Info($"Installing model {model.Path}");
            await invokeAiApi
                .InstallModel(
                    new InstallModelRequest
                    {
                        Name = Path.GetFileNameWithoutExtension(model.Path),
                        Description = Path.GetFileName(model.Path),
                    },
                    source: model.Path,
                    inplace: true
                )
                .ConfigureAwait(false);
        }

        var installStatus = await invokeAiApi.GetModelInstallStatus().ConfigureAwait(false);

        var installCheckCount = 0;

        while (
            !installStatus.All(x =>
                (x.Status != null && x.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                || (x.Status != null && x.Status.Equals("error", StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            installCheckCount++;
            if (installCheckCount > 5)
            {
                onConsoleOutput?.Invoke(
                    new ProcessOutput
                    {
                        Text =
                            "This may take awhile, feel free to use the web interface while the rest of your models are imported.\n",
                    }
                );

                onConsoleOutput?.Invoke(s);

                WebUrl = match.Value;
                OnStartupComplete(WebUrl);

                break;
            }

            onConsoleOutput?.Invoke(
                new ProcessOutput
                {
                    Text =
                        $"\nWaiting for model import... ({installStatus.Count(x => (x.Status != null && !x.Status.Equals("completed",
                        StringComparison.OrdinalIgnoreCase)) && !x.Status.Equals("error", StringComparison.OrdinalIgnoreCase))} remaining)\n",
                }
            );
            await Task.Delay(5000).ConfigureAwait(false);
            try
            {
                installStatus = await invokeAiApi.GetModelInstallStatus().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get model install status");
            }
        }

        return installCheckCount > 5;
    }

    private ImmutableDictionary<string, string> GetEnvVars(
        ImmutableDictionary<string, string> env,
        DirectoryPath installPath
    )
    {
        // Set additional required environment variables

        // Need to make subdirectory because they store config in the
        // directory *above* the root directory
        var root = installPath.JoinDir(RelativeRootPath);
        root.Create();
        env = env.SetItem("INVOKEAI_ROOT", root);

        var path = env.GetValueOrDefault("PATH", string.Empty);

        if (string.IsNullOrEmpty(path))
        {
            path += $"{Compat.PathDelimiter}{Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs")}";
        }
        else
        {
            path += Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs");
        }

        path += $"{Compat.PathDelimiter}{Path.Combine(installPath, "node_modules", ".bin")}";

        if (Compat.IsMacOS || Compat.IsLinux)
        {
            path +=
                $"{Compat.PathDelimiter}{Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs", "bin")}";
        }

        if (Compat.IsWindows)
        {
            path += $"{Compat.PathDelimiter}{Environment.GetFolderPath(Environment.SpecialFolder.System)}";
        }

        return env.SetItem("PATH", path);
    }
}
