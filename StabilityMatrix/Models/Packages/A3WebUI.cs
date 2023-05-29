using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace StabilityMatrix.Models.Packages;

public class A3WebUI : BasePackage
{
    private readonly IGithubApi githubApi;
    private readonly ISettingsManager settingsManager;
    private PyVenvRunner? venvRunner;

    public A3WebUI(IGithubApi githubApi, ISettingsManager settingsManager)
    {
        this.githubApi = githubApi;
        this.settingsManager = settingsManager;
    }
    
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "stable-diffusion-webui";
    public override string Author => "AUTOMATIC1111";
    public override string GithubUrl => "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
    public override string LaunchCommand => "launch.py";
    public override string DefaultLaunchArguments => $"{GetVramOption()} {GetXformersOption()}";

    public override bool UpdateAvailable { get; set; } = false;

    public override async Task<IEnumerable<string>> GetVersions()
    {
        var allReleases = await githubApi.GetAllReleases(Author, Name);
        return allReleases.Select(release => release.TagName!);
    }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new LaunchOptionDefinition
        {
            Name = "API",
            Options = new List<string> { "--api" }
        },
        new LaunchOptionDefinition
        {
            Name = "VRAM",
            Options = new List<string> { "--lowvram", "--medvram" }
        },
        new LaunchOptionDefinition
        {
            Name = "Xformers",
            Options = new List<string> { "--xformers" }
        }
    };

    public override string DownloadLocation =>
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}.zip";

    public override string InstallLocation { get; set; } =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages";


    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private string webUrl = string.Empty;

    public A3WebUI(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }

    public override async Task<string?> DownloadPackage(bool isUpdate = false, string? version = null)
    {
        var latestRelease = await githubApi.GetLatestRelease("AUTOMATIC1111", "stable-diffusion-webui");
        var tagName = string.IsNullOrWhiteSpace(version) ? latestRelease.TagName : version;
        var downloadUrl = $"https://api.github.com/repos/AUTOMATIC1111/stable-diffusion-webui/zipball/{tagName}";

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
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            await file.WriteAsync(buffer, 0, bytesRead);

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

        return tagName;
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

    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        var venvPath = Path.Combine(installedPackagePath, "venv");

        // Setup venv
        venvRunner?.Dispose();
        venvRunner = new PyVenvRunner(venvPath);
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }

        void HandleConsoleOutput(string? s)
        {
            if (s == null) return;
            if (s.Contains("model loaded", StringComparison.OrdinalIgnoreCase))
            {
                OnStartupComplete(webUrl);
            }
            if (s.Contains("Running on", StringComparison.OrdinalIgnoreCase))
            {
                webUrl = s.Split(" ")[5];
            }
            Debug.WriteLine($"process stdout: {s}");
            OnConsoleOutput($"{s}\n");
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnConsoleOutput($"Venv process exited with code {i}");
        }

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";

        venvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit, workingDirectory: installedPackagePath);
    }

    public override Task Shutdown()
    {
        venvRunner?.Dispose();
        venvRunner?.Process?.WaitForExitAsync();
        return Task.CompletedTask;
    }

    public override async Task<bool> CheckForUpdates(string installedPackageName)
    {
        var currentVersion = settingsManager.Settings.InstalledPackages.FirstOrDefault(x => x.Name == installedPackageName)
            ?.PackageVersion;
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        try
        {
            var latestRelease = await githubApi.GetLatestRelease("AUTOMATIC1111", "stable-diffusion-webui");
            UpdateAvailable = latestRelease.TagName != currentVersion;
            return latestRelease.TagName != currentVersion;
        }
        catch (ApiException e)
        {
            Logger.Error(e, "Failed to check for updates");
            return false;
        }
    }

    public override async Task<string?> Update()
    {
        var version = await DownloadPackage(true);
        await InstallPackage(true);

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

    private static string GetVramOption()
    {
        var vramGb = HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024;

        return vramGb switch
        {
            < 4 => "--lowvram",
            < 8 => "--medvram",
            _ => string.Empty
        };
    }

    private static string GetXformersOption()
    {
        var gpuName = HardwareHelper.GetGpuChipName();
        return gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "--xformers" : string.Empty;
    }
}
