﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;
using NLog;
using Python.Runtime;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("windows")]
public class WindowsPrerequisiteHelper(
    IDownloadService downloadService,
    ISettingsManager settingsManager,
    IPyRunner pyRunner,
    IPyInstallationManager pyInstallationManager
) : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string PortableGitDownloadUrl =
        "https://github.com/git-for-windows/git/releases/download/v2.41.0.windows.1/PortableGit-2.41.0-64-bit.7z.exe";

    private const string VcRedistDownloadUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";

    private const string TkinterDownloadUrl =
        "https://cdn.lykos.ai/tkinter-cpython-embedded-3.10.11-win-x64.zip";

    private const string NodeDownloadUrl = "https://nodejs.org/dist/v20.11.0/node-v20.11.0-win-x64.zip";

    private const string Dotnet7DownloadUrl =
        "https://download.visualstudio.microsoft.com/download/pr/2133b143-9c4f-4daa-99b0-34fa6035d67b/193ede446d922eb833f1bfe0239be3fc/dotnet-sdk-7.0.405-win-x64.zip";

    private const string Dotnet8DownloadUrl =
        "https://download.visualstudio.microsoft.com/download/pr/6902745c-34bd-4d66-8e84-d5b61a17dfb7/e61732b00f7e144e162d7e6914291f16/dotnet-sdk-8.0.101-win-x64.zip";

    private const string CppBuildToolsUrl = "https://aka.ms/vs/17/release/vs_BuildTools.exe";

    private const string HipSdkDownloadUrl = "https://cdn.lykos.ai/AMD-HIP-SDK.exe";

    private string HomeDir => settingsManager.LibraryDir;

    private string VcRedistDownloadPath => Path.Combine(HomeDir, "vcredist.x64.exe");

    private string AssetsDir => Path.Combine(HomeDir, "Assets");
    private string SevenZipPath => Path.Combine(AssetsDir, "7za.exe");

    private string GetPythonDownloadPath(PyVersion version) =>
        Path.Combine(
            AssetsDir,
            version == PyInstallationManager.Python_3_10_11
                ? "python-3.10.11-embed-amd64.zip"
                : $"python-{version}-amd64.tar.gz"
        );

    private string GetPythonDir(PyVersion version) =>
        Path.Combine(AssetsDir, $"Python{version.Major}{version.Minor}{version.Micro}");

    private string GetPythonDllPath(PyVersion version) =>
        Path.Combine(GetPythonDir(version), $"python{version.Major}{version.Minor}.dll");

    private string GetPythonLibraryZipPath(PyVersion version) =>
        Path.Combine(GetPythonDir(version), $"python{version.Major}{version.Minor}.zip");

    private string GetPipPath(PyVersion version) => Path.Combine(GetPythonDir(version), "get-pip.pyc");

    // Temporary directory to extract venv to during python install
    private string GetVenvTempDir(PyVersion version) => Path.Combine(GetPythonDir(version), "venv");

    private string PortableGitInstallDir => Path.Combine(HomeDir, "PortableGit");
    private string PortableGitDownloadPath => Path.Combine(HomeDir, "PortableGit.7z.exe");
    private string GitExePath => Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    private string TkinterZipPath => Path.Combine(AssetsDir, "tkinter.zip");
    private string TkinterExtractPath => Path.Combine(AssetsDir, "Python31011"); // Updated from Python310 to Python31011
    private string TkinterExistsPath => Path.Combine(TkinterExtractPath, "tkinter");
    private string NodeExistsPath => Path.Combine(AssetsDir, "nodejs", "npm.cmd");
    private string NodeDownloadPath => Path.Combine(AssetsDir, "nodejs.zip");
    private string Dotnet7DownloadPath => Path.Combine(AssetsDir, "dotnet-sdk-7.0.405-win-x64.zip");
    private string Dotnet8DownloadPath => Path.Combine(AssetsDir, "dotnet-sdk-8.0.101-win-x64.zip");
    private string DotnetExtractPath => Path.Combine(AssetsDir, "dotnet");
    private string DotnetExistsPath => Path.Combine(DotnetExtractPath, "dotnet.exe");
    private string VcBuildToolsDownloadPath => Path.Combine(AssetsDir, "vs_BuildTools.exe");

    private string VcBuildToolsExistsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "2022",
            "BuildTools"
        );

    private string HipSdkDownloadPath => Path.Combine(AssetsDir, "AMD-HIP-SDK.exe");

    private string HipInstalledPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AMD", "ROCm", "5.7");

    public string GitBinPath => Path.Combine(PortableGitInstallDir, "bin");

    // Check if a specific Python version is installed
    public bool IsPythonVersionInstalled(PyVersion version) => File.Exists(GetPythonDllPath(version));

    // Legacy property for compatibility
    public bool IsPythonInstalled => IsPythonVersionInstalled(PyInstallationManager.DefaultVersion);

    // Implement interface method - uses default Python version
    public Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        return InstallPythonIfNecessary(PyInstallationManager.DefaultVersion, progress);
    }

    // Implement interface method - uses default Python version
    public Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        return InstallTkinterIfNecessary(PyInstallationManager.DefaultVersion, progress);
    }

    public async Task RunGit(
        ProcessArgs args,
        Action<ProcessOutput>? onProcessOutput,
        string? workingDirectory = null
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            GitExePath,
            args,
            workingDirectory,
            onProcessOutput,
            environmentVariables: new Dictionary<string, string>
            {
                { "PATH", Compat.GetEnvPathWithExtensions(GitBinPath) }
            }
        );
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new ProcessException($"Git exited with code {process.ExitCode}");
        }
    }

    public async Task RunGit(ProcessArgs args, string? workingDirectory = null)
    {
        var result = await ProcessRunner
            .GetProcessResultAsync(GitExePath, args, workingDirectory)
            .ConfigureAwait(false);

        result.EnsureSuccessExitCode();
    }

    public Task<ProcessResult> GetGitOutput(ProcessArgs args, string? workingDirectory = null)
    {
        return ProcessRunner.GetProcessResultAsync(
            GitExePath,
            args,
            workingDirectory: workingDirectory,
            environmentVariables: new Dictionary<string, string>
            {
                { "PATH", Compat.GetEnvPathWithExtensions(GitBinPath) }
            }
        );
    }

    public async Task RunNpm(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null
    )
    {
        var result = await ProcessRunner
            .GetProcessResultAsync(NodeExistsPath, args, workingDirectory, envVars)
            .ConfigureAwait(false);

        result.EnsureSuccessExitCode();
        onProcessOutput?.Invoke(ProcessOutput.FromStdOutLine(result.StandardOutput));
        onProcessOutput?.Invoke(ProcessOutput.FromStdErrLine(result.StandardError));
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

        if (prerequisites.Contains(PackagePrerequisite.Python31016))
        {
            await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_16, progress);
            await InstallVirtualenvIfNecessary(PyInstallationManager.Python_3_10_16, progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Git))
        {
            await InstallGitIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.VcRedist))
        {
            await InstallVcRedistIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Node))
        {
            await InstallNodeIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Dotnet))
        {
            await InstallDotnetIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.Tkinter))
        {
            await InstallTkinterIfNecessary(PyInstallationManager.Python_3_10_11, progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.VcBuildTools))
        {
            await InstallVcBuildToolsIfNecessary(progress);
        }

        if (prerequisites.Contains(PackagePrerequisite.HipSdk))
        {
            await InstallHipSdkIfNecessary(progress);
        }
    }

    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await InstallVcRedistIfNecessary(progress);
        await UnpackResourcesIfNecessary(progress);
        await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_11, progress);
        await InstallPythonIfNecessary(PyInstallationManager.Python_3_10_16, progress);
        await InstallGitIfNecessary(progress);
        await InstallNodeIfNecessary(progress);
        await InstallVcBuildToolsIfNecessary(progress);
        await InstallHipSdkIfNecessary(progress);
    }

    public async Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // Array of (asset_uri, extract_to)
        var assets = new[] { (Assets.SevenZipExecutable, AssetsDir), (Assets.SevenZipLicense, AssetsDir), };

        progress?.Report(new ProgressReport(0, message: "Unpacking resources", isIndeterminate: true));

        Directory.CreateDirectory(AssetsDir);
        foreach (var (asset, extractDir) in assets)
        {
            await asset.ExtractToDir(extractDir);
        }

        progress?.Report(new ProgressReport(1, message: "Unpacking resources", isIndeterminate: false));
    }

    public async Task InstallPythonIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null)
    {
        var pythonDllPath = GetPythonDllPath(version);

        if (File.Exists(pythonDllPath))
        {
            Logger.Debug("Python {Version} already installed at {PythonDllPath}", version, pythonDllPath);
            return;
        }

        Logger.Info("Python {Version} not found at {PythonDllPath}, downloading...", version, pythonDllPath);

        Directory.CreateDirectory(AssetsDir);

        var pythonLibraryZipPath = GetPythonLibraryZipPath(version);

        // Delete existing python zip if it exists
        if (File.Exists(pythonLibraryZipPath))
        {
            File.Delete(pythonLibraryZipPath);
        }

        // Get the correct download URL for this Python version
        RemoteResource? remote = null;
        if (version == PyInstallationManager.Python_3_10_11)
        {
            remote = Assets.PythonDownloadUrl;
        }
        else if (version == PyInstallationManager.Python_3_10_16)
        {
            remote = Assets.Python3_10_16DownloadUrl;
        }
        else
        {
            throw new ArgumentException($"No download URL configured for Python {version}");
        }

        var url = remote.Value.Url.ToString();
        var pythonDownloadPath = GetPythonDownloadPath(version);

        Logger.Info($"Downloading Python {version} from {url} to {pythonDownloadPath}");

        // Cleanup to remove zip if download fails
        try
        {
            // Download python zip
            await downloadService.DownloadToFileAsync(url, pythonDownloadPath, progress: progress);

            // Verify python hash
            var downloadHash = await FileHash.GetSha256Async(pythonDownloadPath, progress);
            if (downloadHash != remote.Value.HashSha256)
            {
                var fileExists = File.Exists(pythonDownloadPath);
                var fileSize = new FileInfo(pythonDownloadPath).Length;
                var msg =
                    $"Python download hash mismatch: {downloadHash} != {remote.Value.HashSha256} "
                    + $"(file exists: {fileExists}, size: {fileSize})";
                throw new Exception(msg);
            }

            progress?.Report(
                new ProgressReport(progress: 1f, message: $"Python {version} download complete")
            );

            progress?.Report(
                new ProgressReport(-1, $"Installing Python {version}...", isIndeterminate: true)
            );

            // We also need 7z if it's not already unpacked
            if (!File.Exists(SevenZipPath))
            {
                await Assets.SevenZipExecutable.ExtractToDir(AssetsDir);
                await Assets.SevenZipLicense.ExtractToDir(AssetsDir);
            }

            var pythonDir = GetPythonDir(version);

            // Delete existing python dir
            if (Directory.Exists(pythonDir))
            {
                Directory.Delete(pythonDir, true);
            }

            // For Python 3.10.11, we need to handle embedded venv folder
            if (version == PyInstallationManager.Python_3_10_11)
            {
                try
                {
                    // Extract embedded venv folder
                    var venvTempDir = GetVenvTempDir(version);
                    Directory.CreateDirectory(venvTempDir);
                    foreach (var (resource, relativePath) in Assets.PyModuleVenv)
                    {
                        var path = Path.Combine(venvTempDir, relativePath);
                        // Create missing directories
                        var dir = Path.GetDirectoryName(path);
                        if (dir != null)
                        {
                            Directory.CreateDirectory(dir);
                        }

                        await resource.ExtractTo(path);
                    }
                    // Add venv to python's library zip
                    await ArchiveHelper.AddToArchive7Z(pythonLibraryZipPath, venvTempDir);
                }
                finally
                {
                    // Remove venv
                    var venvTempDir = GetVenvTempDir(version);
                    if (Directory.Exists(venvTempDir))
                    {
                        Directory.Delete(venvTempDir, true);
                    }
                }
            }

            // Unzip python
            if (version == PyInstallationManager.Python_3_10_11)
            {
                await ArchiveHelper.Extract7Z(pythonDownloadPath, pythonDir);
                // We need to uncomment the #import site line in python._pth for pip to work
                var pythonPthPath = Path.Combine(pythonDir, $"python{version.Major}{version.Minor}._pth");
                var pythonPthContent = await File.ReadAllTextAsync(pythonPthPath);
                pythonPthContent = pythonPthContent.Replace("#import site", "import site");
                await File.WriteAllTextAsync(pythonPthPath, pythonPthContent);

                // Install TKinter
                await InstallTkinterIfNecessary(version, progress);
            }
            else
            {
                await ArchiveHelper.Extract7ZTar(pythonDownloadPath, pythonDir);
                // it gets extracted into a folder named `python`, we need to move the contents to this pythonDir
                var extractedDir = Path.Combine(pythonDir, "python");
                if (Directory.Exists(extractedDir))
                {
                    foreach (var file in Directory.GetFiles(extractedDir))
                    {
                        var destFile = Path.Combine(pythonDir, Path.GetFileName(file));
                        File.Move(file, destFile);
                    }

                    foreach (var dir in Directory.GetDirectories(extractedDir))
                    {
                        var destDir = Path.Combine(pythonDir, Path.GetFileName(dir));
                        Directory.Move(dir, destDir);
                    }

                    Directory.Delete(extractedDir, true);
                }
            }

            // Extract get-pip.pyc
            await Assets.PyScriptGetPip.ExtractToDir(pythonDir);
            progress?.Report(new ProgressReport(1f, $"Python {version} install complete"));
        }
        finally
        {
            // Always delete zip after download
            if (File.Exists(pythonDownloadPath))
            {
                File.Delete(pythonDownloadPath);
            }
        }
    }

    public async Task InstallVirtualenvIfNecessary(
        PyVersion version,
        IProgress<ProgressReport>? progress = null
    )
    {
        var installation = pyInstallationManager.GetInstallation(version);

        // Check if pip and venv are installed for this version
        if (!installation.PipInstalled || !installation.VenvInstalled)
        {
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Installing Python {version} prerequisites...",
                    isIndeterminate: true
                )
            );

            // Switch to this version if needed
            if (PythonEngine.IsInitialized)
            {
                await pyRunner.SwitchToInstallation(version).ConfigureAwait(false);
            }
            else
            {
                // Initialize with this version
                await pyRunner.Initialize().ConfigureAwait(false);
                await pyRunner.SwitchToInstallation(version).ConfigureAwait(false);
            }

            if (!installation.PipInstalled)
            {
                await pyRunner.SetupPip(version).ConfigureAwait(false);
            }

            if (!installation.VenvInstalled)
            {
                await pyRunner.InstallPackage("virtualenv", version).ConfigureAwait(false);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallTkinterIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null)
    {
        var pythonDir = GetPythonDir(version);
        var tkinterPath = Path.Combine(pythonDir, "tkinter");

        if (!Directory.Exists(tkinterPath))
        {
            Logger.Info($"Downloading Tkinter for Python {version}");
            await downloadService.DownloadToFileAsync(TkinterDownloadUrl, TkinterZipPath, progress: progress);
            progress?.Report(
                new ProgressReport(
                    progress: 1f,
                    message: "Tkinter download complete",
                    type: ProgressType.Download
                )
            );

            await ArchiveHelper.Extract(TkinterZipPath, pythonDir, progress);

            File.Delete(TkinterZipPath);
        }

        progress?.Report(
            new ProgressReport(progress: 1f, message: "Tkinter install complete", type: ProgressType.Generic)
        );
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(GitExePath))
        {
            Logger.Debug("Git already installed at {GitExePath}", GitExePath);
            return;
        }

        Logger.Info("Git not found at {GitExePath}, downloading...", GitExePath);

        // Download
        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(
                PortableGitDownloadUrl,
                PortableGitDownloadPath,
                progress: progress
            );
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);

        await FixGitLongPaths();
    }

    [SupportedOSPlatform("windows")]
    public async Task<bool> FixGitLongPaths()
    {
        if (!Compat.IsWindows)
            return false;

        try
        {
            await RunGit(["config", "--system", "core.longpaths", "true"]);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to set git longpaths");
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64", false);
        if (key != null)
        {
            var buildId = Convert.ToUInt32(key.GetValue("Bld"));
            if (buildId >= 30139)
            {
                return;
            }
        }

        Logger.Info("Downloading VC Redist");

        await downloadService.DownloadToFileAsync(
            VcRedistDownloadUrl,
            VcRedistDownloadPath,
            progress: progress
        );
        progress?.Report(
            new ProgressReport(
                progress: 1f,
                message: "Visual C++ download complete",
                type: ProgressType.Download
            )
        );

        Logger.Info("Installing VC Redist");
        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing prerequisites..."
            )
        );
        var process = ProcessRunner.StartAnsiProcess(VcRedistDownloadPath, "/install /quiet /norestart");
        await process.WaitForExitAsync();
        progress?.Report(
            new ProgressReport(
                progress: 1f,
                message: "Visual C++ install complete",
                type: ProgressType.Generic
            )
        );

        File.Delete(VcRedistDownloadPath);
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallNodeIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(NodeExistsPath))
            return;

        await DownloadAndExtractPrerequisite(progress, NodeDownloadUrl, NodeDownloadPath, AssetsDir);

        var extractedNodeDir = Path.Combine(AssetsDir, "node-v20.11.0-win-x64");
        if (Directory.Exists(extractedNodeDir))
        {
            Directory.Move(extractedNodeDir, Path.Combine(AssetsDir, "nodejs"));
        }
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallDotnetIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(DotnetExistsPath))
            return;

        await DownloadAndExtractPrerequisite(
            progress,
            Dotnet7DownloadUrl,
            Dotnet7DownloadPath,
            DotnetExtractPath
        );
        await DownloadAndExtractPrerequisite(
            progress,
            Dotnet8DownloadUrl,
            Dotnet8DownloadPath,
            DotnetExtractPath
        );
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallVcBuildToolsIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (
            Directory.Exists(VcBuildToolsExistsPath)
            && Directory.EnumerateDirectories(VcBuildToolsExistsPath).Any()
        )
            return;

        await downloadService.DownloadToFileAsync(
            CppBuildToolsUrl,
            VcBuildToolsDownloadPath,
            progress: progress
        );

        Logger.Info("Installing VC Build Tools");
        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing prerequisites..."
            )
        );

        var process = ProcessRunner.StartAnsiProcess(
            VcBuildToolsDownloadPath,
            "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended",
            outputDataReceived: output =>
                progress?.Report(
                    new ProgressReport(
                        progress: 0.5f,
                        isIndeterminate: true,
                        type: ProgressType.Generic,
                        message: output.ApcMessage?.Data ?? output.Text
                    )
                )
        );
        await process.WaitForExitAsync();
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallHipSdkIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (Directory.Exists(HipInstalledPath))
            return;

        await downloadService.DownloadToFileAsync(HipSdkDownloadUrl, HipSdkDownloadPath, progress: progress);
        Logger.Info("Downloaded & installing HIP SDK");

        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing HIP SDK, this may take a few minutes..."
            )
        );

        var info = new ProcessStartInfo
        {
            FileName = HipSdkDownloadPath,
            Arguments = "-install -log hip_install.log",
            UseShellExecute = true,
            CreateNoWindow = true,
            Verb = "runas"
        };

        if (Process.Start(info) is { } process)
        {
            await process.WaitForExitAsync();
        }
    }

    public async Task<Process> RunDotnet(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null,
        bool waitForExit = true
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            DotnetExistsPath,
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

    private async Task DownloadAndExtractPrerequisite(
        IProgress<ProgressReport>? progress,
        string downloadUrl,
        string downloadPath,
        string extractPath
    )
    {
        Logger.Info($"Downloading {downloadUrl} to {downloadPath}");
        await downloadService.DownloadToFileAsync(downloadUrl, downloadPath, progress: progress);

        Logger.Info("Extracting prerequisite");
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
        await ArchiveHelper.Extract(downloadPath, extractPath, progress);

        progress?.Report(
            new ProgressReport(
                progress: 1f,
                message: "Prerequisite install complete",
                type: ProgressType.Generic
            )
        );

        File.Delete(downloadPath);
    }

    private async Task UnzipGit(IProgress<ProgressReport>? progress = null)
    {
        if (progress == null)
        {
            await ArchiveHelper.Extract7Z(PortableGitDownloadPath, PortableGitInstallDir);
        }
        else
        {
            await ArchiveHelper.Extract7Z(PortableGitDownloadPath, PortableGitInstallDir, progress);
        }

        Logger.Info("Extracted Git");

        File.Delete(PortableGitDownloadPath);
    }
}
