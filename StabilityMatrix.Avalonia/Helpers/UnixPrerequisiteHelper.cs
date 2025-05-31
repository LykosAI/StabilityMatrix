using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("macos")]
[SupportedOSPlatform("linux")]
public class UnixPrerequisiteHelper(
    IDownloadService downloadService,
    ISettingsManager settingsManager,
    IPyRunner pyRunner
) : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string UvMacDownloadUrl =
        "https://github.com/astral-sh/uv/releases/download/0.7.3/uv-aarch64-apple-darwin.tar.gz";
    private const string UvLinuxDownloadUrl =
        "https://github.com/astral-sh/uv/releases/download/0.7.3/uv-x86_64-unknown-linux-gnu.tar.gz";

    private DirectoryPath HomeDir => settingsManager.LibraryDir;
    private DirectoryPath AssetsDir => HomeDir.JoinDir("Assets");

    // Helper method to get Python directory for specific version
    private DirectoryPath GetPythonDir(PyVersion version) =>
        AssetsDir.JoinDir($"Python{version.Major}{version.Minor}{version.Micro}");

    // Helper method to check if specific Python version is installed
    private bool IsPythonVersionInstalled(PyVersion version) =>
        GetPythonDir(version).JoinFile(PyRunner.RelativePythonDllPath).Exists;

    // Legacy property for compatibility
    public bool IsPythonInstalled => IsPythonVersionInstalled(PyInstallationManager.DefaultVersion);
    private DirectoryPath PortableGitInstallDir => HomeDir + "PortableGit";
    public string GitBinPath => PortableGitInstallDir + "bin";

    private DirectoryPath NodeDir => AssetsDir.JoinDir("nodejs");
    private string NpmPath => Path.Combine(NodeDir, "bin", "npm");
    private bool IsNodeInstalled => File.Exists(NpmPath);

    private DirectoryPath DotnetDir => AssetsDir.JoinDir("dotnet");
    private string DotnetPath => Path.Combine(DotnetDir, "dotnet");
    private string Dotnet7SdkExistsPath => Path.Combine(DotnetDir, "sdk", "7.0.405");
    private string Dotnet8SdkExistsPath => Path.Combine(DotnetDir, "sdk", "8.0.101");
    private string Dotnet7DownloadUrlMacOs =>
        "https://download.visualstudio.microsoft.com/download/pr/5bb0e0e4-2a8d-4aba-88ad-232e1f65c281/ee6d35f762d81965b4cf336edde1b318/dotnet-sdk-7.0.405-osx-arm64.tar.gz";
    private string Dotnet8DownloadUrlMacOs =>
        "https://download.visualstudio.microsoft.com/download/pr/ef083c06-7aee-4a4f-b18b-50c9a8990753/e206864e7910e81bbd9cb7e674ff1b4c/dotnet-sdk-8.0.101-osx-arm64.tar.gz";
    private string Dotnet7DownloadUrlLinux =>
        "https://download.visualstudio.microsoft.com/download/pr/5202b091-2406-445c-b40a-68a5b97c882b/b509f2a7a0eb61aea145b990b40b6d5b/dotnet-sdk-7.0.405-linux-x64.tar.gz";
    private string Dotnet8DownloadUrlLinux =>
        "https://download.visualstudio.microsoft.com/download/pr/9454f7dc-b98e-4a64-a96d-4eb08c7b6e66/da76f9c6bc4276332b587b771243ae34/dotnet-sdk-8.0.101-linux-x64.tar.gz";

    // Cached store of whether or not git is installed
    private bool? isGitInstalled;

    public bool IsVcBuildToolsInstalled => false;
    public bool IsHipSdkInstalled => false;
    private string UvDownloadPath => Path.Combine(AssetsDir, "uv.tar.gz");
    private string UvExtractPath => Path.Combine(AssetsDir, "uv");
    public string UvExePath => Path.Combine(UvExtractPath, "uv");
    public bool IsUvInstalled => File.Exists(UvExePath);

    // Helper method to get Python download URL for a specific version
    private RemoteResource GetPythonDownloadResource(PyVersion version)
    {
        if (version == PyInstallationManager.Python_3_10_11)
        {
            return Assets.PythonDownloadUrl;
        }

        throw new ArgumentException($"Unsupported Python version: {version}", nameof(version));
    }

    // Helper method to get download path for a specific Python version
    private string GetPythonDownloadPath(PyVersion version) =>
        Path.Combine(AssetsDir, $"python-{version}-amd64.tar.gz");

    private async Task<bool> CheckIsGitInstalled()
    {
        var result = await ProcessRunner.RunBashCommand("git --version");
        isGitInstalled = result.ExitCode == 0;
        return isGitInstalled == true;
    }

    public Task InstallPackageRequirements(BasePackage package, IProgress<ProgressReport>? progress = null) =>
        InstallPackageRequirements(package.Prerequisites.ToList(), progress);

    public async Task InstallPackageRequirements(
        List<PackagePrerequisite> prerequisites,
        IProgress<ProgressReport>? progress = null
    )
    {
        await UnpackResourcesIfNecessary(progress);

        if (prerequisites.Contains(PackagePrerequisite.Python310))
        {
            await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_11, progress);
            await InstallVirtualenvIfNecessary(PyInstallationManager.Python_3_10_11, progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Python31017))
        {
            await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_17, progress);
            await InstallVirtualenvIfNecessary(PyInstallationManager.Python_3_10_17, progress);
        }

        await InstallUvIfNecessary(progress);

        if (prerequisites.Contains(PackagePrerequisite.Git))
        {
            await InstallGitIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Node))
        {
            await InstallNodeIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Dotnet))
        {
            await InstallDotnetIfNecessary(progress);
        }
    }

    public async Task InstallDotnetIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var downloadUrl = Compat.IsMacOS ? Dotnet8DownloadUrlMacOs : Dotnet8DownloadUrlLinux;

        var dotnet8SdkExists = Directory.Exists(Dotnet8SdkExistsPath);

        if (dotnet8SdkExists && Directory.Exists(DotnetDir))
        {
            Logger.Info("Dotnet 8 SDK already installed at {DotnetDir}", DotnetDir);
            return;
        }

        await DownloadAndExtractPrerequisite(progress, downloadUrl, DotnetDir);
    }

    private async Task InstallVirtualenvIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // python stuff
        if (!PyRunner.PipInstalled || !PyRunner.VenvInstalled)
        {
            progress?.Report(
                new ProgressReport(-1f, "Installing Python prerequisites...", isIndeterminate: true)
            );

            await pyRunner.Initialize().ConfigureAwait(false);

            if (!PyRunner.PipInstalled)
            {
                await pyRunner.SetupPip().ConfigureAwait(false);
            }
            if (!PyRunner.VenvInstalled)
            {
                await pyRunner.InstallPackage("virtualenv").ConfigureAwait(false);
            }
        }
    }

    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await UnpackResourcesIfNecessary(progress);
        await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_11, progress);
        await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_17, progress);
        await InstallUvIfNecessary(progress);
    }

    public async Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // Array of (asset_uri, extract_to)
        var assets = new[] { (Assets.SevenZipExecutable, AssetsDir), (Assets.SevenZipLicense, AssetsDir) };

        progress?.Report(new ProgressReport(0, message: "Unpacking resources", isIndeterminate: true));

        Directory.CreateDirectory(AssetsDir);
        foreach (var (asset, extractDir) in assets)
        {
            await asset.ExtractToDir(extractDir);
        }

        progress?.Report(new ProgressReport(1, message: "Unpacking resources", isIndeterminate: false));
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (isGitInstalled == true || (isGitInstalled == null && await CheckIsGitInstalled()))
            return;

        // Show prompt to install git
        var dialog = new ContentDialog
        {
            Title = "Git not found",
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "The current operation requires Git. Please install it to continue.",
                    },
                    new SelectableTextBlock { Text = "$ sudo apt install git" },
                },
            },
            PrimaryButtonText = Resources.Action_Retry,
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Primary,
        };

        while (true)
        {
            // Return if installed
            if (await CheckIsGitInstalled())
                return;
            if (await dialog.ShowAsync() == ContentDialogResult.None)
            {
                // Cancel
                throw new OperationCanceledException("Git installation canceled");
            }
            // Otherwise continue to retry indefinitely
        }
    }

    /// <inheritdoc />
    public Task RunGit(
        ProcessArgs args,
        Action<ProcessOutput>? onProcessOutput = null,
        string? workingDirectory = null
    )
    {
        // Async progress not supported on Unix
        return RunGit(args, workingDirectory);
    }

    private async Task RunGit(ProcessArgs args, string? workingDirectory = null)
    {
        var command = args.Prepend("git");

        var result = await ProcessRunner.RunBashCommand(command, workingDirectory ?? "");
        if (result.ExitCode != 0)
        {
            Logger.Error(
                "Git command [{Command}] failed with exit code " + "{ExitCode}:\n{StdOut}\n{StdErr}",
                command,
                result.ExitCode,
                result.StandardOutput,
                result.StandardError
            );

            throw new ProcessException(
                $"Git command [{command}] failed with exit code"
                    + $" {result.ExitCode}:\n{result.StandardOutput}\n{result.StandardError}"
            );
        }
    }

    public async Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await InstallPythonIfNecessary(PyInstallationManager.DefaultVersion, progress);
    }

    public async Task InstallPythonIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null)
    {
        var pythonDir = GetPythonDir(version);

        if (IsPythonVersionInstalled(version))
            return;

        Directory.CreateDirectory(AssetsDir);

        // Download
        var remote = GetPythonDownloadResource(version);
        var url = remote.Url;
        var hashSha256 = remote.HashSha256;

        var fileName = Path.GetFileName(url.LocalPath);
        var downloadPath = Path.Combine(AssetsDir, fileName);
        Logger.Info($"Downloading Python {version} from {url} to {downloadPath}");
        try
        {
            await downloadService.DownloadToFileAsync(url.ToString(), downloadPath, progress);

            // Verify hash
            var actualHash = await FileHash.GetSha256Async(downloadPath);
            Logger.Info($"Verifying Python {version} hash: (expected: {hashSha256}, actual: {actualHash})");
            if (actualHash != hashSha256)
            {
                throw new Exception(
                    $"Python {version} download hash mismatch: expected {hashSha256}, actual {actualHash}"
                );
            }

            // Extract
            Logger.Info($"Extracting Python {version} Zip: {downloadPath} to {pythonDir}");
            if (pythonDir.Exists)
            {
                await pythonDir.DeleteAsync(true);
            }
            progress?.Report(new ProgressReport(0, $"Installing Python {version}", isIndeterminate: true));
            await ArchiveHelper.Extract7ZAuto(downloadPath, pythonDir);

            // For Unix, move the inner 'python' folder up to the root PythonDir
            if (Compat.IsUnix)
            {
                var innerPythonDir = pythonDir.JoinDir("python");
                if (!innerPythonDir.Exists)
                {
                    throw new Exception(
                        $"Python {version} download did not contain expected inner 'python' folder: {innerPythonDir}"
                    );
                }

                foreach (var folder in Directory.EnumerateDirectories(innerPythonDir))
                {
                    var folderName = Path.GetFileName(folder);
                    var dest = Path.Combine(pythonDir, folderName);
                    Directory.Move(folder, dest);
                }
                Directory.Delete(innerPythonDir);
            }
        }
        finally
        {
            // Cleanup download file
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }
        }

        // Initialize pyrunner and install virtualenv
        await pyRunner.Initialize();
        await pyRunner.SwitchToInstallation(version);
        await pyRunner.InstallPackage("virtualenv", version);

        progress?.Report(new ProgressReport(1, $"Installing Python {version}", isIndeterminate: false));
    }

    public Task<ProcessResult> GetGitOutput(ProcessArgs args, string? workingDirectory = null)
    {
        return ProcessRunner.RunBashCommand(args.Prepend("git"), workingDirectory ?? "");
    }

    [Localizable(false)]
    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    public async Task RunNpm(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            NpmPath,
            args,
            workingDirectory,
            onProcessOutput,
            envVars
        );

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return;

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();

        Logger.Error(
            "RunNpm with args [{Args}] failed with exit code " + "{ExitCode}:\n{StdOut}\n{StdErr}",
            args,
            process.ExitCode,
            stdOut,
            stdErr
        );

        throw new ProcessException(
            $"RunNpm with args [{args}] failed with exit code" + $" {process.ExitCode}:\n{stdOut}\n{stdErr}"
        );
    }

    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    public async Task<Process> RunDotnet(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null,
        bool waitForExit = true
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            DotnetPath,
            args,
            workingDirectory,
            onProcessOutput,
            envVars
        );

        if (!waitForExit)
            return process;

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return process;

        Logger.Error(
            "dotnet8 with args [{Args}] failed with exit code " + "{ExitCode}:\n{StdOut}\n{StdErr}",
            args,
            process.ExitCode,
            process.StandardOutput,
            process.StandardError
        );

        throw new ProcessException(
            $"dotnet8 with args [{args}] failed with exit code"
                + $" {process.ExitCode}:\n{process.StandardOutput}\n{process.StandardError}"
        );
    }

    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    public async Task InstallNodeIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (IsNodeInstalled)
        {
            Logger.Info("node already installed");
            return;
        }

        Logger.Info("Downloading node");

        var downloadUrl = Compat.IsMacOS
            ? "https://nodejs.org/dist/v20.11.0/node-v20.11.0-darwin-arm64.tar.gz"
            : "https://nodejs.org/dist/v20.11.0/node-v20.11.0-linux-x64.tar.gz";

        var nodeDownloadPath = AssetsDir.JoinFile(Path.GetFileName(downloadUrl));

        await downloadService.DownloadToFileAsync(downloadUrl, nodeDownloadPath, progress: progress);

        Logger.Info("Installing node");
        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing prerequisites..."
            )
        );

        // unzip
        await ArchiveHelper.Extract7ZAuto(nodeDownloadPath, AssetsDir);

        var nodeDir = Compat.IsMacOS
            ? AssetsDir.JoinDir("node-v20.11.0-darwin-arm64")
            : AssetsDir.JoinDir("node-v20.11.0-linux-x64");
        Directory.Move(nodeDir, NodeDir);

        progress?.Report(
            new ProgressReport(progress: 1f, message: "Node install complete", type: ProgressType.Generic)
        );

        File.Delete(nodeDownloadPath);
    }

    [SupportedOSPlatform("Linux")]
    [SupportedOSPlatform("macOS")]
    public async Task InstallVirtualenvIfNecessary(
        PyVersion version,
        IProgress<ProgressReport>? progress = null
    )
    {
        // Check if pip and venv are installed for this version
        var pipInstalled = File.Exists(Path.Combine(GetPythonDir(version), "bin", "pip3"));
        var venvInstalled = Directory.Exists(
            Path.Combine(GetPythonDir(version), "Scripts", "virtualenv" + Compat.ExeExtension)
        );

        if (!pipInstalled || !venvInstalled)
        {
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Installing Python {version} prerequisites...",
                    isIndeterminate: true
                )
            );

            await pyRunner.Initialize();
            await pyRunner.SwitchToInstallation(version);

            if (!pipInstalled)
            {
                await pyRunner.SetupPip(version).ConfigureAwait(false);
            }

            if (!venvInstalled)
            {
                await pyRunner.InstallPackage("virtualenv", version).ConfigureAwait(false);
            }
        }
    }

    public async Task InstallUvIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (IsUvInstalled)
        {
            Logger.Debug("UV already installed at {UvExePath}", UvExePath);
            return;
        }

        Logger.Info("UV not found at {UvExePath}, downloading...", UvExePath);

        Directory.CreateDirectory(AssetsDir);

        var downloadUrl = Compat.IsMacOS ? UvMacDownloadUrl : UvLinuxDownloadUrl;

        // Download UV archive
        await downloadService.DownloadToFileAsync(downloadUrl, UvDownloadPath, progress: progress);

        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing UV package manager..."
            )
        );

        // Create extraction directory
        Directory.CreateDirectory(UvExtractPath);

        // Extract UV
        await ArchiveHelper.Extract7ZTar(UvDownloadPath, UvExtractPath);

        // On Mac/Linux, the extraction might create a platform-specific folder
        // (e.g., uv-aarch64-apple-darwin or uv-x86_64-unknown-linux-gnu)
        // We need to move both the uv and uvx executables from that folder to the expected location

        // Find platform-specific directory
        var platformSpecificDir = Directory
            .GetDirectories(UvExtractPath)
            .FirstOrDefault(dir => Path.GetFileName(dir).StartsWith("uv-"));

        if (platformSpecificDir != null)
        {
            Logger.Debug("Found platform-specific UV directory: {PlatformDir}", platformSpecificDir);

            // List of files to move: uv and uvx
            var filesToMove = new Dictionary<string, string>
            {
                { Path.Combine(platformSpecificDir, "uv"), Path.Combine(UvExtractPath, "uv") },
                { Path.Combine(platformSpecificDir, "uvx"), Path.Combine(UvExtractPath, "uvx") },
            };

            var anyFilesMoved = false;

            // Move each file if it exists
            foreach (var entry in filesToMove)
            {
                var sourcePath = entry.Key;
                var destPath = entry.Value;

                if (File.Exists(sourcePath))
                {
                    Logger.Debug("Moving file from {Source} to {Destination}", sourcePath, destPath);

                    // Ensure the destination doesn't exist before moving
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }

                    File.Move(sourcePath, destPath);
                    anyFilesMoved = true;

                    // Make the executable file executable
                    var process = ProcessRunner.StartAnsiProcess("chmod", ["+x", destPath]);
                    await process.WaitForExitAsync();
                }
            }

            // Delete the now-empty platform directory after moving all files
            if (anyFilesMoved)
            {
                Directory.Delete(platformSpecificDir, true);
            }
        }
        else if (File.Exists(UvExePath))
        {
            // For Windows or if we already have the file in the right place, just make it executable
            var process = ProcessRunner.StartAnsiProcess("chmod", ["+x", UvExePath]);
            await process.WaitForExitAsync();
        }

        progress?.Report(
            new ProgressReport(progress: 1f, message: "UV installation complete", type: ProgressType.Generic)
        );

        // Clean up download
        File.Delete(UvDownloadPath);
    }

    private async Task DownloadAndExtractPrerequisite(
        IProgress<ProgressReport>? progress,
        string downloadUrl,
        string extractPath
    )
    {
        Logger.Info($"Downloading {downloadUrl}");

        var downloadPath = AssetsDir.JoinFile(Path.GetFileName(downloadUrl));

        await downloadService.DownloadToFileAsync(downloadUrl, downloadPath, progress: progress);

        Logger.Info("Installing prereq");
        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing prerequisites..."
            )
        );

        Directory.CreateDirectory(extractPath);

        // unzip
        await ArchiveHelper.Extract7ZAuto(downloadPath, extractPath);

        progress?.Report(
            new ProgressReport(progress: 1f, message: "Node install complete", type: ProgressType.Generic)
        );

        File.Delete(downloadPath);
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task<bool> FixGitLongPaths()
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task AddMissingLibsToVenv(
        DirectoryPath installedPackagePath,
        PyBaseInstall baseInstall,
        IProgress<ProgressReport>? progress = null
    )
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallTkinterIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }
}
