using System;
using System.Threading.Tasks;

namespace StabilityMatrix.Models;

public abstract class BasePackage
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string Author { get; }
    public abstract string GithubUrl { get; }
    public abstract string LaunchCommand { get; }
    public abstract string DefaultLaunchArguments { get; }
    public virtual bool UpdateAvailable { get; set; }
    public abstract Task<string?> DownloadPackage(bool isUpdate = false);
    public abstract Task InstallPackage(bool isUpdate = false);
    public abstract Task RunPackage(string installedPackagePath, string arguments);
    public abstract Task Shutdown();
    public abstract Task<bool> CheckForUpdates();
    public abstract Task<string?> Update();

    internal virtual string DownloadLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}.zip";
    internal virtual string InstallLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}";

    public event EventHandler<int>? DownloadProgressChanged;
    public event EventHandler<string>? DownloadComplete;
    public event EventHandler<int>? InstallProgressChanged;
    public event EventHandler<string>? InstallComplete;
    public event EventHandler<int>? UpdateProgressChanged;
    public event EventHandler<string>? UpdateComplete;
    public event EventHandler<string>? ConsoleOutput;
    public event EventHandler<int>? Exited;
    public event EventHandler<string>? StartupComplete;

    public void OnDownloadProgressChanged(int progress) => DownloadProgressChanged?.Invoke(this, progress);
    public void OnDownloadComplete(string path) => DownloadComplete?.Invoke(this, path);
    public void OnInstallProgressChanged(int progress) => InstallProgressChanged?.Invoke(this, progress);
    public void OnInstallComplete(string path) => InstallComplete?.Invoke(this, path);
    public void OnConsoleOutput(string output) => ConsoleOutput?.Invoke(this, output);
    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);
    public void OnStartupComplete(string url) => StartupComplete?.Invoke(this, url);
    public void OnUpdateProgressChanged(int progress) => UpdateProgressChanged?.Invoke(this, progress);
    public void OnUpdateComplete(string path) => UpdateComplete?.Invoke(this, path);
    

    public string ByAuthor => $"By {Author}";
}
