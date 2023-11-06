using System.Globalization;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class InvokeAI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string RelativeRootPath = "invokeai-root";

    public override string Name => "InvokeAI";
    public override string DisplayName { get; set; } = "InvokeAI";
    public override string Author => "invoke-ai";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/invoke-ai/InvokeAI/blob/main/LICENSE";

    public override string Blurb => "Professional Creative Tools for Stable Diffusion";
    public override string LaunchCommand => "invokeai-web";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Nightmare;

    public override IReadOnlyList<string> ExtraLaunchCommands =>
        new[]
        {
            "invokeai-configure",
            "invokeai-merge",
            "invokeai-metadata",
            "invokeai-model-install",
            "invokeai-node-cli",
            "invokeai-ti",
            "invokeai-update",
        };

    public override Uri PreviewImageUri =>
        new(
            "https://raw.githubusercontent.com/invoke-ai/InvokeAI/main/docs/assets/canvas_preview.png"
        );

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override string MainBranch => "main";

    public InvokeAI(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "main")
            },
            [SharedFolderType.Lora] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "lora")
            },
            [SharedFolderType.TextualInversion] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "embedding")
            },
            [SharedFolderType.ControlNet] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "controlnet")
            },
            [SharedFolderType.IpAdapter] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "ip_adapter")
            }
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Text2Img] = new[]
            {
                Path.Combine("invokeai-root", "outputs", "images")
            }
        };

    public override string OutputFolderName => Path.Combine("invokeai-root", "outputs", "images");

    // https://github.com/invoke-ai/InvokeAI/blob/main/docs/features/CONFIGURATION.md
    public override List<LaunchOptionDefinition> LaunchOptions =>
        new List<LaunchOptionDefinition>
        {
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = new List<string> { "--host" }
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "9090",
                Options = new List<string> { "--port" }
            },
            new()
            {
                Name = "Allow Origins",
                Description =
                    "List of host names or IP addresses that are allowed to connect to the "
                    + "InvokeAI API in the format ['host1','host2',...]",
                Type = LaunchOptionType.String,
                DefaultValue = "[]",
                Options = new List<string> { "--allow-origins" }
            },
            new()
            {
                Name = "Always use CPU",
                Type = LaunchOptionType.Bool,
                Options = new List<string> { "--always_use_cpu" }
            },
            new()
            {
                Name = "Precision",
                Type = LaunchOptionType.Bool,
                Options = new List<string>
                {
                    "--precision auto",
                    "--precision float16",
                    "--precision float32",
                }
            },
            new()
            {
                Name = "Aggressively free up GPU memory after each operation",
                Type = LaunchOptionType.Bool,
                Options = new List<string> { "--free_gpu_mem" }
            },
            LaunchOptionDefinition.Extras
        };

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.Rocm, TorchVersion.Mps };

    public override TorchVersion GetRecommendedTorchVersion()
    {
        if (Compat.IsMacOS && Compat.IsArm)
        {
            return TorchVersion.Mps;
        }

        return base.GetRecommendedTorchVersion();
    }

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        // Setup venv
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        var venvPath = Path.Combine(installLocation, "venv");
        var exists = Directory.Exists(venvPath);

        await using var venvRunner = new PyVenvRunner(venvPath);
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);

        venvRunner.EnvironmentVariables = GetEnvVars(installLocation);
        progress?.Report(new ProgressReport(-1f, "Installing Package", isIndeterminate: true));

        var pipCommandArgs =
            "-e . --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu";

        switch (torchVersion)
        {
            // If has Nvidia Gpu, install CUDA version
            case TorchVersion.Cuda:
                await InstallCudaTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                Logger.Info("Starting InvokeAI install (CUDA)...");
                pipCommandArgs =
                    "-e .[xformers] --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu118";
                break;
            // For AMD, Install ROCm version
            case TorchVersion.Rocm:
                await venvRunner
                    .PipInstall(
                        new PipInstallArgs()
                            .WithTorch("==2.0.1")
                            .WithTorchVision()
                            .WithExtraIndex("rocm5.4.2"),
                        onConsoleOutput
                    )
                    .ConfigureAwait(false);
                Logger.Info("Starting InvokeAI install (ROCm)...");
                pipCommandArgs =
                    "-e . --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.4.2";
                break;
            case TorchVersion.Mps:
                // For Apple silicon, use MPS
                Logger.Info("Starting InvokeAI install (MPS)...");
                pipCommandArgs = "-e . --use-pep517";
                break;
        }

        await venvRunner
            .PipInstall($"{pipCommandArgs}{(exists ? " --upgrade" : "")}", onConsoleOutput)
            .ConfigureAwait(false);

        await venvRunner
            .PipInstall("rich packaging python-dotenv", onConsoleOutput)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Configuring InvokeAI", isIndeterminate: true));

        // need to setup model links before running invokeai-configure so it can do its conversion
        await SetupModelFolders(installLocation, selectedSharedFolderMethod).ConfigureAwait(false);

        await RunInvokeCommand(
                installLocation,
                "invokeai-configure",
                "--yes --skip-sd-weights",
                true,
                onConsoleOutput,
                spam3: true
            )
            .ConfigureAwait(false);

        await VenvRunner.Process.WaitForExitAsync();

        progress?.Report(new ProgressReport(1f, "Done!", isIndeterminate: false));
    }

    public override Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    ) => RunInvokeCommand(installedPackagePath, command, arguments, true, onConsoleOutput);

    private async Task RunInvokeCommand(
        string installedPackagePath,
        string command,
        string arguments,
        bool runDetached,
        Action<ProcessOutput>? onConsoleOutput,
        bool spam3 = false
    )
    {
        if (spam3 && !runDetached)
        {
            throw new InvalidOperationException("Cannot spam 3 if not running detached");
        }

        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        arguments = command switch
        {
            "invokeai-configure" => "--yes --skip-sd-weights",
            _ => arguments
        };

        VenvRunner.EnvironmentVariables = GetEnvVars(installedPackagePath);

        // Launch command is for a console entry point, and not a direct script
        var entryPoint = await VenvRunner.GetEntryPoint(command).ConfigureAwait(false);

        // Split at ':' to get package and function
        var split = entryPoint?.Split(':');

        if (split is not { Length: > 1 })
        {
            throw new Exception($"Could not find entry point for InvokeAI: {entryPoint.ToRepr()}");
        }

        // Compile a startup command according to
        // https://packaging.python.org/en/latest/specifications/entry-points/#use-for-scripts
        // For invokeai, also patch the shutil.get_terminal_size function to return a fixed value
        // above the minimum in invokeai.frontend.install.widgets

        var code = $"""
                    try:
                        import os
                        import shutil
                        from invokeai.frontend.install import widgets
                        
                        _min_cols = widgets.MIN_COLS
                        _min_lines = widgets.MIN_LINES
                        
                        static_size_fn = lambda: os.terminal_size((_min_cols, _min_lines))
                        shutil.get_terminal_size = static_size_fn
                        widgets.get_terminal_size = static_size_fn
                    except Exception as e:
                        import warnings
                        warnings.warn('Could not patch terminal size for InvokeAI' + str(e))
                        
                    import sys
                    from {split[0]} import {split[1]}
                    sys.exit({split[1]}())
                    """;

        if (runDetached)
        {
            var foundPrompt = false;

            void HandleConsoleOutput(ProcessOutput s)
            {
                onConsoleOutput?.Invoke(s);

                if (
                    spam3
                    && s.Text.Contains(
                        "[3] Accept the best guess;  you can fix it in the Web UI later",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    foundPrompt = true;
                    Task.Delay(100)
                        .ContinueWith(_ => VenvRunner.Process?.StandardInput.WriteLine("3"));
                    return;
                }

                if (foundPrompt)
                {
                    VenvRunner.Process?.StandardInput.WriteLine("3");
                    foundPrompt = false;
                }

                if (!s.Text.Contains("running on", StringComparison.OrdinalIgnoreCase))
                    return;

                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (!match.Success)
                    return;

                WebUrl = match.Value;
                OnStartupComplete(WebUrl);
            }

            VenvRunner.RunDetached(
                $"-c \"{code}\" {arguments}".TrimEnd(),
                HandleConsoleOutput,
                OnExit
            );
        }
        else
        {
            var result = await VenvRunner
                .Run($"-c \"{code}\" {arguments}".TrimEnd())
                .ConfigureAwait(false);
            onConsoleOutput?.Invoke(new ProcessOutput { Text = result.StandardOutput });
        }
    }

    private Dictionary<string, string> GetEnvVars(DirectoryPath installPath)
    {
        // Set additional required environment variables
        var env = new Dictionary<string, string>();
        if (SettingsManager.Settings.EnvironmentVariables is not null)
        {
            env.Update(SettingsManager.Settings.EnvironmentVariables);
        }

        // Need to make subdirectory because they store config in the
        // directory *above* the root directory
        var root = installPath.JoinDir(RelativeRootPath);
        root.Create();
        env["INVOKEAI_ROOT"] = root;

        return env;
    }
}
