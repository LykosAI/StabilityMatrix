using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Octokit;
using StabilityMatrix.Models;
using StabilityMatrix.Python;
using StabilityMatrix.Services;

namespace StabilityMatrix.Helper;

public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;
    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private const string VcRedistDownloadUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";
    
    private static readonly string VcRedistDownloadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "vcredist.x64.exe");

    private static readonly string PortableGitInstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit");

    private static readonly string PortableGitDownloadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit.7z.exe");
    
    private static readonly string GitExePath = Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    
    public static readonly string GitBinPath = Path.Combine(PortableGitInstallDir, "bin");

    public PrerequisiteHelper(ILogger<PrerequisiteHelper> logger, IGitHubClient gitHubClient,
        IDownloadService downloadService, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.gitHubClient = gitHubClient;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(GitExePath))
        {
            logger.LogDebug($"Git already installed at {GitExePath}");
            return;
        }
        
        logger.LogInformation($"Git not found at {GitExePath}, downloading...");

        var latestRelease = await gitHubClient.Repository.Release.GetLatest("git-for-windows", "git");
        var portableGitUrl = latestRelease.Assets
            .First(a => a.Name.EndsWith("64-bit.7z.exe")).BrowserDownloadUrl;

        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(portableGitUrl, PortableGitDownloadPath, progress: progress);
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);
    }

    public async Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64", false);
        if (key != null)
        {
            var buildId = Convert.ToUInt32(key.GetValue("Bld"));
            if (buildId >= 30139)
            {
                return;
            }
        }
        
        logger.LogInformation("Downloading VC Redist");

        await downloadService.DownloadToFileAsync(VcRedistDownloadUrl, VcRedistDownloadPath, progress: progress);
        progress?.Report(new ProgressReport(progress: 1f, message: "Visual C++ download complete",
            type: ProgressType.Download));
        
        logger.LogInformation("Installing VC Redist");
        progress?.Report(new ProgressReport(progress: 0.5f, isIndeterminate: true, type: ProgressType.Generic, message: "Installing prerequisites..."));
        var process = ProcessRunner.StartProcess(VcRedistDownloadPath, "/install /quiet /norestart");
        await process.WaitForExitAsync();
        progress?.Report(new ProgressReport(progress: 1f, message: "Visual C++ install complete",
            type: ProgressType.Generic));
        
        File.Delete(VcRedistDownloadPath);
    }

    public async Task SetupPythonDependencies(string installLocation, string requirementsFileName,
        IProgress<ProgressReport>? progress = null, Action<string?>? onConsoleOutput = null)
    {
        // Setup dependencies
        progress?.Report(new ProgressReport(-1, isIndeterminate: true));
        var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));

        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }

        void HandleConsoleOutput(string? s)
        {
            Debug.WriteLine($"venv stdout: {s}");
            onConsoleOutput?.Invoke(s);
        }

        // Install torch
        logger.LogDebug("Starting torch install...");
        await venvRunner.PipInstall(venvRunner.GetTorchInstallCommand(), installLocation, HandleConsoleOutput);

        // Install xformers if nvidia
        if (HardwareHelper.HasNvidiaGpu())
        {
            await venvRunner.PipInstall("xformers", installLocation, HandleConsoleOutput);
        }

        // Install requirements
        logger.LogDebug("Starting requirements install...");
        await venvRunner.PipInstall($"-r {requirementsFileName}", installLocation, HandleConsoleOutput);

        logger.LogDebug("Finished installing requirements!");
        progress?.Report(new ProgressReport(1, isIndeterminate: false));
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

        logger.LogInformation("Extracted Git");

        File.Delete(PortableGitDownloadPath);
        // Also add git to the path
        settingsManager.AddPathExtension(GitBinPath);
        settingsManager.InsertPathExtensions();
    }

}
