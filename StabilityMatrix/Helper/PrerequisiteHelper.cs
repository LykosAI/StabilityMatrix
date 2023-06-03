using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Services;

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
            "PortableGit.7z.exe");
    
    private static readonly string GitExePath = Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    
    public static readonly string GitBinPath = Path.Combine(PortableGitInstallDir, "bin");

    public PrerequisiteHelper(ILogger<PrerequisiteHelper> logger, IGitHubClient gitHubClient,
        IDownloadService downloadService)
    {
        this.logger = logger;
        this.gitHubClient = gitHubClient;
        this.downloadService = downloadService;
    }

    public async Task<Process?> InstallGitIfNecessary()
    {
        if (File.Exists(GitExePath))
        {
            logger.LogDebug($"Git already installed at {GitExePath}");
            return null;
        }
        
        logger.LogInformation($"Git not found at {GitExePath}, downloading...");

        var latestRelease =await gitHubClient.Repository.Release.GetLatest("git-for-windows", "git");
        var portableGitUrl = latestRelease.Assets
            .First(a => a.Name.StartsWith("PortableGit") && a.Name.Contains("64-bit")).BrowserDownloadUrl;

        await downloadService.DownloadToFileAsync(portableGitUrl, PortableGitDownloadPath);

        var installProcess =
            ProcessRunner.StartProcess(PortableGitDownloadPath, $"-o \"{PortableGitInstallDir}\" -y -gm2");
        installProcess.OutputDataReceived += (_, args) => { logger.LogDebug(args.Data); };

        return installProcess;
    }
}
