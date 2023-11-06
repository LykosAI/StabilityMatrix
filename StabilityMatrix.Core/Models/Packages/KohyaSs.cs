using System.Text.RegularExpressions;
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
public class KohyaSs : BaseGitPackage
{
    public KohyaSs(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

    public override string Name => "kohya_ss";
    public override string DisplayName { get; set; } = "kohya_ss";
    public override string Author => "bmaltais";
    public override string Blurb =>
        "A Windows-focused Gradio GUI for Kohya's Stable Diffusion trainers";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl =>
        "https://github.com/bmaltais/kohya_ss/blob/master/LICENSE.md";
    public override string LaunchCommand => "kohya_gui.py";

    public override Uri PreviewImageUri =>
        new(
            "https://camo.githubusercontent.com/2170d2204816f428eec57ff87218f06344e0b4d91966343a6c5f0a76df91ec75/68747470733a2f2f696d672e796f75747562652e636f6d2f76692f6b35696d713031757655592f302e6a7067"
        );
    public override string OutputFolderName => string.Empty;

    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();

    public override TorchVersion GetRecommendedTorchVersion() => TorchVersion.Cuda;

    public override string Disclaimer =>
        "Nvidia GPU with at least 8GB VRAM is recommended. May be unstable on Linux.";

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.UltraNightmare;

    public override bool OfferInOneClickInstaller => false;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;
    public override IEnumerable<TorchVersion> AvailableTorchVersions => new[] { TorchVersion.Cuda };
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.None };

    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
            new LaunchOptionDefinition
            {
                Name = "Listen Address",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = new List<string> { "--listen" }
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Options = new List<string> { "--port" }
            },
            new LaunchOptionDefinition
            {
                Name = "Username",
                Type = LaunchOptionType.String,
                Options = new List<string> { "--username" }
            },
            new LaunchOptionDefinition
            {
                Name = "Password",
                Type = LaunchOptionType.String,
                Options = new List<string> { "--password" }
            },
            new LaunchOptionDefinition
            {
                Name = "Auto-Launch Browser",
                Type = LaunchOptionType.Bool,
                Options = new List<string> { "--inbrowser" }
            },
            new LaunchOptionDefinition
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Options = new List<string> { "--share" }
            },
            new LaunchOptionDefinition
            {
                Name = "Headless",
                Type = LaunchOptionType.Bool,
                Options = new List<string> { "--headless" }
            },
            new LaunchOptionDefinition
            {
                Name = "Language",
                Type = LaunchOptionType.String,
                Options = new List<string> { "--language" }
            },
            LaunchOptionDefinition.Extras
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
        if (Compat.IsWindows)
        {
            progress?.Report(
                new ProgressReport(-1f, "Installing prerequisites...", isIndeterminate: true)
            );
            await PrerequisiteHelper.InstallTkinterIfNecessary(progress).ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);

        if (Compat.IsWindows)
        {
            var setupSmPath = Path.Combine(installLocation, "setup", "setup_sm.py");
            var setupText = """
                            import setup_windows
                            import setup_common

                            setup_common.install_requirements('requirements_windows_torch2.txt', check_no_verify_flag=False)
                            setup_windows.sync_bits_and_bytes_files()
                            setup_common.configure_accelerate(run_accelerate=False)
                            """;
            await File.WriteAllTextAsync(setupSmPath, setupText).ConfigureAwait(false);

            // Install
            venvRunner.RunDetached("setup/setup_sm.py", onConsoleOutput);
            await venvRunner.Process.WaitForExitAsync().ConfigureAwait(false);
        }
        else if (Compat.IsLinux)
        {
            venvRunner.RunDetached(
                "setup/setup_linux.py --platform-requirements-file=requirements_linux.txt --no_run_accelerate",
                onConsoleOutput
            );
            await venvRunner.Process.WaitForExitAsync().ConfigureAwait(false);
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

        // update gui files to point to venv accelerate
        var filesToUpdate = new[]
        {
            "lora_gui.py",
            "dreambooth_gui.py",
            "textual_inversion_gui.py",
            Path.Combine("library", "wd14_caption_gui.py"),
            "finetune_gui.py"
        };

        foreach (var file in filesToUpdate)
        {
            var path = Path.Combine(installedPackagePath, file);
            var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var replacementAcceleratePath = Compat.IsWindows
                ? @".\\venv\\scripts\\accelerate"
                : "./venv/bin/accelerate";
            text = text.Replace(
                "run_cmd = f'accelerate launch",
                $"run_cmd = f'{replacementAcceleratePath} launch"
            );
            await File.WriteAllTextAsync(path, text).ConfigureAwait(false);
        }

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

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner.EnvironmentVariables = GetEnvVars(installedPackagePath);
        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, OnExit);
    }

    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<
        SharedOutputType,
        IReadOnlyList<string>
    >? SharedOutputFolders { get; }

    public override string MainBranch => "master";

    private Dictionary<string, string> GetEnvVars(string installDirectory)
    {
        // Set additional required environment variables
        var env = new Dictionary<string, string>();
        if (SettingsManager.Settings.EnvironmentVariables is not null)
        {
            env.Update(SettingsManager.Settings.EnvironmentVariables);
        }

        if (!Compat.IsWindows)
            return env;

        var tkPath = Path.Combine(
            SettingsManager.LibraryDir,
            "Assets",
            "Python310",
            "tcl",
            "tcl8.6"
        );
        env["TCL_LIBRARY"] = tkPath;
        env["TK_LIBRARY"] = tkPath;

        return env;
    }
}
