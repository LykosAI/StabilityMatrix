using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;
using NLog;
using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("windows")]
public class WindowsPrerequisiteHelper : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;

    private const string VcRedistDownloadUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";

    private string HomeDir => settingsManager.LibraryDir;

    private string VcRedistDownloadPath => Path.Combine(HomeDir, "vcredist.x64.exe");

    private string AssetsDir => Path.Combine(HomeDir, "Assets");
    private string SevenZipPath => Path.Combine(AssetsDir, "7za.exe");

    private string PythonDownloadPath => Path.Combine(AssetsDir, "python-3.10.11-embed-amd64.zip");
    private string PythonDir => Path.Combine(AssetsDir, "Python310");
    private string PythonDllPath => Path.Combine(PythonDir, "python310.dll");
    private string PythonLibraryZipPath => Path.Combine(PythonDir, "python310.zip");
    private string GetPipPath => Path.Combine(PythonDir, "get-pip.pyc");

    // Temporary directory to extract venv to during python install
    private string VenvTempDir => Path.Combine(PythonDir, "venv");

    private string PortableGitInstallDir => Path.Combine(HomeDir, "PortableGit");
    private string PortableGitDownloadPath => Path.Combine(HomeDir, "PortableGit.7z.exe");
    private string GitExePath => Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    private string TkinterZipPath => Path.Combine(AssetsDir, "tkinter.zip");
    private string TkinterExtractPath => PythonDir;
    public string GitBinPath => Path.Combine(PortableGitInstallDir, "bin");

    public bool IsPythonInstalled => File.Exists(PythonDllPath);

    public WindowsPrerequisiteHelper(
        IGitHubClient gitHubClient,
        IDownloadService downloadService,
        ISettingsManager settingsManager
    )
    {
        this.gitHubClient = gitHubClient;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
    }

    public async Task RunGit(
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        params string[] args
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            GitExePath,
            args,
            workingDirectory: workingDirectory,
            environmentVariables: new Dictionary<string, string>
            {
                { "PATH", Compat.GetEnvPathWithExtensions(GitBinPath) }
            },
            outputDataReceived: onProcessOutput
        );

        await ProcessRunner.WaitForExitConditionAsync(process);
    }

    public async Task<string> GetGitOutput(string? workingDirectory = null, params string[] args)
    {
        var process = await ProcessRunner.GetProcessOutputAsync(
            GitExePath,
            string.Join(" ", args),
            workingDirectory: workingDirectory,
            environmentVariables: new Dictionary<string, string>
            {
                { "PATH", Compat.GetEnvPathWithExtensions(GitBinPath) }
            }
        );

        return process;
    }

    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await InstallVcRedistIfNecessary(progress);
        await UnpackResourcesIfNecessary(progress);
        await InstallPythonIfNecessary(progress);
        await InstallGitIfNecessary(progress);
    }

    public async Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // Array of (asset_uri, extract_to)
        var assets = new[]
        {
            (Assets.SevenZipExecutable, AssetsDir),
            (Assets.SevenZipLicense, AssetsDir),
        };

        progress?.Report(
            new ProgressReport(0, message: "Unpacking resources", isIndeterminate: true)
        );

        Directory.CreateDirectory(AssetsDir);
        foreach (var (asset, extractDir) in assets)
        {
            await asset.ExtractToDir(extractDir);
        }

        progress?.Report(
            new ProgressReport(1, message: "Unpacking resources", isIndeterminate: false)
        );
    }

    public async Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(PythonDllPath))
        {
            Logger.Debug("Python already installed at {PythonDllPath}", PythonDllPath);
            return;
        }

        Logger.Info("Python not found at {PythonDllPath}, downloading...", PythonDllPath);

        Directory.CreateDirectory(AssetsDir);

        // Delete existing python zip if it exists
        if (File.Exists(PythonLibraryZipPath))
        {
            File.Delete(PythonLibraryZipPath);
        }

        var remote = Assets.PythonDownloadUrl;
        var url = remote.Url.ToString();
        Logger.Info($"Downloading Python from {url} to {PythonLibraryZipPath}");

        // Cleanup to remove zip if download fails
        try
        {
            // Download python zip
            await downloadService.DownloadToFileAsync(url, PythonDownloadPath, progress: progress);

            // Verify python hash
            var downloadHash = await FileHash.GetSha256Async(PythonDownloadPath, progress);
            if (downloadHash != remote.HashSha256)
            {
                var fileExists = File.Exists(PythonDownloadPath);
                var fileSize = new FileInfo(PythonDownloadPath).Length;
                var msg =
                    $"Python download hash mismatch: {downloadHash} != {remote.HashSha256} "
                    + $"(file exists: {fileExists}, size: {fileSize})";
                throw new Exception(msg);
            }

            progress?.Report(new ProgressReport(progress: 1f, message: "Python download complete"));

            progress?.Report(new ProgressReport(-1, "Installing Python...", isIndeterminate: true));

            // We also need 7z if it's not already unpacked
            if (!File.Exists(SevenZipPath))
            {
                await Assets.SevenZipExecutable.ExtractToDir(AssetsDir);
                await Assets.SevenZipLicense.ExtractToDir(AssetsDir);
            }

            // Delete existing python dir
            if (Directory.Exists(PythonDir))
            {
                Directory.Delete(PythonDir, true);
            }

            // Unzip python
            await ArchiveHelper.Extract7Z(PythonDownloadPath, PythonDir);

            try
            {
                // Extract embedded venv folder
                Directory.CreateDirectory(VenvTempDir);
                foreach (var (resource, relativePath) in Assets.PyModuleVenv)
                {
                    var path = Path.Combine(VenvTempDir, relativePath);
                    // Create missing directories
                    var dir = Path.GetDirectoryName(path);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                    }

                    await resource.ExtractTo(path);
                }
                // Add venv to python's library zip

                await ArchiveHelper.AddToArchive7Z(PythonLibraryZipPath, VenvTempDir);
            }
            finally
            {
                // Remove venv
                if (Directory.Exists(VenvTempDir))
                {
                    Directory.Delete(VenvTempDir, true);
                }
            }

            // Extract get-pip.pyc
            await Assets.PyScriptGetPip.ExtractToDir(PythonDir);

            // We need to uncomment the #import site line in python310._pth for pip to work
            var pythonPthPath = Path.Combine(PythonDir, "python310._pth");
            var pythonPthContent = await File.ReadAllTextAsync(pythonPthPath);
            pythonPthContent = pythonPthContent.Replace("#import site", "import site");
            await File.WriteAllTextAsync(pythonPthPath, pythonPthContent);

            // Install TKinter
            await InstallTkinterIfNecessary(progress);

            progress?.Report(new ProgressReport(1f, "Python install complete"));
        }
        finally
        {
            // Always delete zip after download
            if (File.Exists(PythonDownloadPath))
            {
                File.Delete(PythonDownloadPath);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (!File.Exists(TkinterZipPath))
        {
            await Assets.TkinterZip.ExtractTo(TkinterZipPath);
        }

        await ArchiveHelper.Extract(TkinterZipPath, TkinterExtractPath, progress);
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(GitExePath))
        {
            Logger.Debug("Git already installed at {GitExePath}", GitExePath);
            return;
        }

        Logger.Info("Git not found at {GitExePath}, downloading...", GitExePath);

        var portableGitUrl =
            "https://github.com/git-for-windows/git/releases/download/v2.41.0.windows.1/PortableGit-2.41.0-64-bit.7z.exe";

        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(
                portableGitUrl,
                PortableGitDownloadPath,
                progress: progress
            );
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64",
            false
        );
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
        var process = ProcessRunner.StartAnsiProcess(
            VcRedistDownloadPath,
            "/install /quiet /norestart"
        );
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
