using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Python;

namespace StabilityMatrix.Models.Packages;

/// <summary>
/// Base class for packages that are hosted on Github.
/// Author and Name should be the Github username and repository name respectively.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class BaseGitPackage : BasePackage
{
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    protected readonly IGithubApiCache GithubApi;
    protected readonly ISettingsManager SettingsManager;
    protected PyVenvRunner? VenvRunner;
    
    /// <summary>
    /// URL of the hosted web page on launch
    /// </summary>
    protected string WebUrl = string.Empty;

    public override string GithubUrl => $"https://github.com/{Author}/{Name}";

    public override string DownloadLocation =>
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}.zip";

    public override string InstallLocation { get; set; } =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages";
    
    protected string GetDownloadUrl(string tagName) => $"https://api.github.com/repos/{Author}/{Name}/zipball/{tagName}";

    protected BaseGitPackage(IGithubApiCache githubApi, ISettingsManager settingsManager)
    {
        this.GithubApi = githubApi;
        this.SettingsManager = settingsManager;
    }

    protected Task<GithubRelease> GetLatestRelease()
    {
        return GithubApi.GetLatestRelease(Author, Name);
    }
    
    public override Task<IEnumerable<GithubBranch>> GetAllBranches()
    {
        return GithubApi.GetAllBranches(Author, Name);
    }
    
    public override Task<IEnumerable<GithubRelease>> GetAllReleases()
    {
        return GithubApi.GetAllReleases(Author, Name);
    }
    
    public override Task<IEnumerable<GithubCommit>> GetAllCommits(string branch, int page = 1, int perPage = 10)
    {
        return GithubApi.GetAllCommits(Author, Name, branch, page, perPage);
    }

    /// <summary>
    /// Setup the virtual environment for the package.
    /// </summary>
    /// <param name="installedPackagePath"></param>
    /// <param name="venvName"></param>
    /// <returns></returns>
    protected async Task<PyVenvRunner> SetupVenv(string installedPackagePath, string venvName = "venv")
    {
        var venvPath = Path.Combine(installedPackagePath, "venv");
        VenvRunner?.Dispose();
        VenvRunner = new PyVenvRunner(venvPath);
        if (!VenvRunner.Exists())
        {
            await VenvRunner.Setup();
        }
        return VenvRunner;
    }
    
    public override async Task<IEnumerable<GithubRelease>> GetReleaseTags()
    {
        var allReleases = await GithubApi.GetAllReleases(Author, Name);
        return allReleases;
    }
    
    public override async Task<string?> DownloadPackage(string version, bool isUpdate = false)
    {
        var downloadUrl = GetDownloadUrl(version);

        if (!Directory.Exists(DownloadLocation.Replace($"{Name}.zip", "")))
        {
            Directory.CreateDirectory(DownloadLocation.Replace($"{Name}.zip", ""));
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "1.0"));
        await using var file = new FileStream(DownloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
        

        long contentLength = 0;
        var retryCount = 0;
        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        while (contentLength == 0 && retryCount++ < 5)
        {
            response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            contentLength = response.Content.Headers.ContentLength ?? 0;
            Logger.Debug("Retrying get-headers for content-length");
            Thread.Sleep(50);
        }

        var isIndeterminate = contentLength == 0;

        await using var stream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0;
        while (true)
        {
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalBytesRead += bytesRead;

            if (isIndeterminate)
            {
                if (isUpdate)
                {
                    OnUpdateProgressChanged(-1);
                }
                else
                {
                    OnDownloadProgressChanged(-1);
                }
            }
            else
            {
                var progress = (int)(totalBytesRead * 100d / contentLength);
                Logger.Debug($"Progress; {progress}");
                
                if (isUpdate)
                {
                    OnUpdateProgressChanged(progress);
                }
                else
                {
                    OnDownloadProgressChanged(progress);
                }
            }
        }

        await file.FlushAsync();
        OnDownloadComplete(DownloadLocation);

        return version;
    }
    
    private void UnzipPackage(bool isUpdate = false)
    {
        if (isUpdate)
        {
            OnInstallProgressChanged(0);
        }
        else
        {
            OnUpdateProgressChanged(0);
        }

        Directory.CreateDirectory(InstallLocation);

        using var zip = ZipFile.OpenRead(DownloadLocation);
        var zipDirName = string.Empty;
        var totalEntries = zip.Entries.Count;
        var currentEntry = 0;

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) && entry.FullName.EndsWith("/"))
            {
                if (string.IsNullOrWhiteSpace(zipDirName))
                {
                    zipDirName = entry.FullName;
                    continue;
                }

                var folderPath = Path.Combine(InstallLocation,
                    entry.FullName.Replace(zipDirName, string.Empty));
                Directory.CreateDirectory(folderPath);
                continue;
            }


            var destinationPath = Path.GetFullPath(Path.Combine(InstallLocation,
                entry.FullName.Replace(zipDirName, string.Empty)));
            entry.ExtractToFile(destinationPath, true);
            currentEntry++;

            var progressValue = (int)((double)currentEntry / totalEntries * 100);

            if (isUpdate)
            {
                OnUpdateProgressChanged(progressValue);
            }
            else
            {
                OnInstallProgressChanged(progressValue);
            }
        }
    }
    
    public override Task InstallPackage(bool isUpdate = false)
    {
        UnzipPackage(isUpdate);
        
        if (isUpdate)
        {
            OnUpdateComplete("Update complete");
        }
        else
        {
            OnInstallComplete("Installation complete");
        }

        return Task.CompletedTask;
    }

    public override async Task<bool> CheckForUpdates(string installedPackageName)
    {
        var currentVersion = SettingsManager.Settings.InstalledPackages.FirstOrDefault(x => x.Name == installedPackageName)
            ?.PackageVersion;
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        try
        {
            var latestVersion = await GetLatestVersion();
            UpdateAvailable = latestVersion != currentVersion;
            return latestVersion != currentVersion;
        }
        catch (ApiException e)
        {
            Logger.Error(e, "Failed to check for updates");
            return false;
        }
    }

    public override async Task<string> Update()
    {
        var version = await GetLatestVersion();
        await DownloadPackage(version, true);
        await InstallPackage(true);
        return version;
    }

    
    public override Task Shutdown()
    {
        VenvRunner?.Dispose();
        VenvRunner?.Process?.WaitForExitAsync();
        return Task.CompletedTask;
    }
}
