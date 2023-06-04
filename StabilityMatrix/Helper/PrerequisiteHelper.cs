using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Octokit;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using StabilityMatrix.Models;
using StabilityMatrix.Services;
using Application = System.Windows.Application;

namespace StabilityMatrix.Helper;

public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;
    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;

    private static readonly string PortableGitInstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit");

    private static readonly string PortableGitDownloadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            "PortableGit.tar.bz2");
    
    private static readonly string GitExePath = Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    
    public static readonly string GitBinPath = Path.Combine(PortableGitInstallDir, "bin");

    public PrerequisiteHelper(ILogger<PrerequisiteHelper> logger, IGitHubClient gitHubClient,
        IDownloadService downloadService)
    {
        this.logger = logger;
        this.gitHubClient = gitHubClient;
        this.downloadService = downloadService;
    }

    public event EventHandler<ProgressReport>? DownloadProgressChanged;
    public event EventHandler<ProgressReport>? DownloadComplete;

    public event EventHandler<ProgressReport>? InstallProgressChanged;
    public event EventHandler<ProgressReport>? InstallComplete;

    public async Task InstallGitIfNecessary()
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
            downloadService.DownloadProgressChanged += OnDownloadProgressChanged;
            downloadService.DownloadComplete += OnDownloadComplete;

            await downloadService.DownloadToFileAsync(portableGitUrl, PortableGitDownloadPath);

            downloadService.DownloadProgressChanged -= OnDownloadProgressChanged;
            downloadService.DownloadComplete -= OnDownloadComplete;
        }

        await UnzipGit();
    }
    
    private async Task UnzipGit()
    {
        var progress = new Progress<ProgressReport>();
        progress.ProgressChanged += OnInstallProgressChanged;

        OnInstallProgressChanged(this, new ProgressReport(-1, isIndeterminate: true));
        await ArchiveHelper.Extract(PortableGitDownloadPath, PortableGitInstallDir, progress);

        logger.LogInformation("Extracted Git");
        File.Delete(PortableGitDownloadPath);
        OnInstallComplete(this, new ProgressReport(progress: 1f));
    }

    private void OnDownloadProgressChanged(object? sender, ProgressReport progress) => DownloadProgressChanged?.Invoke(sender, progress);
    private void OnDownloadComplete(object? sender, ProgressReport progress) => DownloadComplete?.Invoke(sender, progress);
    private void OnInstallProgressChanged(object? sender, ProgressReport progress) => InstallProgressChanged?.Invoke(sender, progress);
    private void OnInstallComplete(object? sender, ProgressReport progress) => InstallComplete?.Invoke(sender, progress);
}
