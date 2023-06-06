using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Models;
using StabilityMatrix.Services;

namespace StabilityMatrix.Helper;

public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;
    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;

    private static readonly string PortableGitInstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit");

    private static readonly string PortableGitDownloadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit.tar.bz2");
    
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

    public event EventHandler<ProgressReport>? InstallProgressChanged;
    public event EventHandler<ProgressReport>? InstallComplete;

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
            .First(a => a.Name.EndsWith("64-bit.tar.bz2")).BrowserDownloadUrl;

        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(portableGitUrl, PortableGitDownloadPath, progress: progress);
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);
    }
    
    private async Task UnzipGit(IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport(-1, isIndeterminate: true, message: ""));
        await ArchiveHelper.Extract(PortableGitDownloadPath, PortableGitInstallDir, progress);

        logger.LogInformation("Extracted Git");

        OnInstallProgressChanged(this, new ProgressReport(-1, isIndeterminate: true));
        File.Delete(PortableGitDownloadPath);
        // Also add git to the path
        settingsManager.AddPathExtension(GitBinPath);
        settingsManager.InsertPathExtensions();
        OnInstallComplete(this, new ProgressReport(progress: 1f));
    }

    private void OnInstallProgressChanged(object? sender, ProgressReport progress) => InstallProgressChanged?.Invoke(sender, progress);
    private void OnInstallComplete(object? sender, ProgressReport progress) => InstallComplete?.Invoke(sender, progress);
}
