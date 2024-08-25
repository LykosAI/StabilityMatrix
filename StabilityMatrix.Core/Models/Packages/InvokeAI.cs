using System.Collections.Immutable;
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
    private string RelativeFrontendBuildPath = Path.Combine("invokeai", "frontend", "web", "dist");

    public override string Name => "InvokeAI";
    public override string DisplayName { get; set; } = "InvokeAI";
    public override string Author => "invoke-ai";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/invoke-ai/InvokeAI/blob/main/LICENSE";

    public override string Blurb => "Professional Creative Tools for Stable Diffusion";
    public override string LaunchCommand => "invokeai-web";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Nightmare;

    public override IReadOnlyList<string> ExtraLaunchCommands =>
        new[] { "invokeai-db-maintenance", "invokeai-import-images", };

    public override Uri PreviewImageUri =>
        new("https://raw.githubusercontent.com/invoke-ai/InvokeAI/main/docs/assets/canvas_preview.png");

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.None };
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;

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
            [SharedFolderType.Lora] = new[] { Path.Combine(RelativeRootPath, "autoimport", "lora") },
            [SharedFolderType.TextualInversion] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "embedding")
            },
            [SharedFolderType.ControlNet] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "controlnet")
            },
            [SharedFolderType.InvokeIpAdapters15] = new[]
            {
                Path.Combine(RelativeRootPath, "models", "sd-1", "ip_adapter")
            },
            [SharedFolderType.InvokeIpAdaptersXl] = new[]
            {
                Path.Combine(RelativeRootPath, "models", "sdxl", "ip_adapter")
            },
            [SharedFolderType.InvokeClipVision] = new[]
            {
                Path.Combine(RelativeRootPath, "models", "any", "clip_vision")
            },
            [SharedFolderType.T2IAdapter] = new[]
            {
                Path.Combine(RelativeRootPath, "autoimport", "t2i_adapter")
            }
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { Path.Combine("invokeai-root", "outputs", "images") } };

    public override string OutputFolderName => Path.Combine("invokeai-root", "outputs", "images");

    // https://github.com/invoke-ai/InvokeAI/blob/main/docs/features/CONFIGURATION.md
    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new LaunchOptionDefinition
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--host"]
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "9090",
                Options = ["--port"]
            },
            new LaunchOptionDefinition
            {
                Name = "Allow Origins",
                Description =
                    "List of host names or IP addresses that are allowed to connect to the "
                    + "InvokeAI API in the format ['host1','host2',...]",
                Type = LaunchOptionType.String,
                DefaultValue = "[]",
                Options = ["--allow-origins"]
            },
            new LaunchOptionDefinition
            {
                Name = "Precision",
                Type = LaunchOptionType.Bool,
                Options = ["--precision auto", "--precision float16", "--precision float32"]
            },
            LaunchOptionDefinition.Extras
        ];

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.Rocm, TorchIndex.Mps };

    public override TorchIndex GetRecommendedTorchVersion()
    {
        if (Compat.IsMacOS && Compat.IsArm)
        {
            return TorchIndex.Mps;
        }

        return base.GetRecommendedTorchVersion();
    }

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
        // Setup venv
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        var venvPath = Path.Combine(installLocation, "venv");
        var exists = Directory.Exists(venvPath);

        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(env => GetEnvVars(env, installLocation));

        progress?.Report(new ProgressReport(-1f, "Installing Package", isIndeterminate: true));

        await SetupAndBuildInvokeFrontend(
                installLocation,
                progress,
                onConsoleOutput,
                venvRunner.EnvironmentVariables
            )
            .ConfigureAwait(false);

        var pipCommandArgs = "-e . --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu";

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var torchInstallArgs = new PipInstallArgs();

        switch (torchVersion)
        {
            case TorchIndex.Cuda:
                torchInstallArgs = torchInstallArgs
                    .WithTorch("==2.2.2")
                    .WithTorchVision("==0.17.2")
                    .WithXFormers("==0.0.25.post1")
                    .WithTorchExtraIndex("cu121");

                Logger.Info("Starting InvokeAI install (CUDA)...");
                pipCommandArgs =
                    "-e .[xformers] --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu121";
                break;

            case TorchIndex.Rocm:
                torchInstallArgs = torchInstallArgs
                    .WithTorch("==2.2.2")
                    .WithTorchVision("==0.17.2")
                    .WithExtraIndex("rocm5.6");

                Logger.Info("Starting InvokeAI install (ROCm)...");
                pipCommandArgs =
                    "-e . --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.6";
                break;

            case TorchIndex.Mps:
                // For Apple silicon, use MPS
                Logger.Info("Starting InvokeAI install (MPS)...");
                pipCommandArgs = "-e . --use-pep517";
                break;
        }

        if (installedPackage.PipOverrides != null)
        {
            torchInstallArgs = torchInstallArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(torchInstallArgs, onConsoleOutput).ConfigureAwait(false);

        await venvRunner
            .PipInstall($"{pipCommandArgs}{(exists ? " --upgrade" : "")}", onConsoleOutput)
            .ConfigureAwait(false);

        await venvRunner.PipInstall("rich packaging python-dotenv", onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Done!", isIndeterminate: false));
    }

    private async Task SetupAndBuildInvokeFrontend(
        string installLocation,
        IProgress<ProgressReport>? progress,
        Action<ProcessOutput>? onConsoleOutput,
        IReadOnlyDictionary<string, string>? envVars = null
    )
    {
        await PrerequisiteHelper.InstallNodeIfNecessary(progress).ConfigureAwait(false);
        await PrerequisiteHelper
            .RunNpm(["i", "pnpm@8"], installLocation, envVars: envVars)
            .ConfigureAwait(false);

        if (Compat.IsMacOS || Compat.IsLinux)
        {
            await PrerequisiteHelper
                .RunNpm(["i", "vite", "--ignore-scripts=true"], installLocation, envVars: envVars)
                .ConfigureAwait(false);
        }

        var pnpmPath = Path.Combine(
            installLocation,
            "node_modules",
            ".bin",
            Compat.IsWindows ? "pnpm.cmd" : "pnpm"
        );

        var vitePath = Path.Combine(
            installLocation,
            "node_modules",
            ".bin",
            Compat.IsWindows ? "vite.cmd" : "vite"
        );

        var invokeFrontendPath = Path.Combine(installLocation, "invokeai", "frontend", "web");

        var process = ProcessRunner.StartAnsiProcess(
            pnpmPath,
            "i --ignore-scripts=true --force",
            invokeFrontendPath,
            onConsoleOutput,
            envVars
        );

        await process.WaitForExitAsync().ConfigureAwait(false);

        process = ProcessRunner.StartAnsiProcess(
            Compat.IsWindows ? pnpmPath : vitePath,
            "build",
            invokeFrontendPath,
            onConsoleOutput,
            envVars
        );

        await process.WaitForExitAsync().ConfigureAwait(false);
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
            new ProcessArgs(options.Arguments.ToArray()),
            true,
            onConsoleOutput
        );

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

        VenvRunner.UpdateEnvironmentVariables(env => GetEnvVars(env, installedPackagePath));

        // fix frontend build missing for people who updated to v3.6 before the fix
        var frontendExistsPath = Path.Combine(installedPackagePath, RelativeFrontendBuildPath);
        if (!Directory.Exists(frontendExistsPath))
        {
            await SetupAndBuildInvokeFrontend(
                    installedPackagePath,
                    null,
                    onConsoleOutput,
                    VenvRunner.EnvironmentVariables
                )
                .ConfigureAwait(false);
        }

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
            void HandleConsoleOutput(ProcessOutput s)
            {
                onConsoleOutput?.Invoke(s);

                if (
                    spam3 && s.Text.Contains("[3] Accept the best guess;", StringComparison.OrdinalIgnoreCase)
                )
                {
                    VenvRunner.Process?.StandardInput.WriteLine("3");
                    return;
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

            VenvRunner.RunDetached($"-c \"{code}\" {arguments}".TrimEnd(), HandleConsoleOutput, OnExit);
        }
        else
        {
            var result = await VenvRunner.Run($"-c \"{code}\" {arguments}".TrimEnd()).ConfigureAwait(false);
            onConsoleOutput?.Invoke(new ProcessOutput { Text = result.StandardOutput });
        }
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
