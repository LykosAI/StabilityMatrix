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


    public abstract Task DownloadPackage();

    public abstract Task InstallPackage();
    public abstract Task RunPackage(string installedPackagePath, string arguments);
    public abstract Task Shutdown();

    internal virtual string DownloadLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}.zip";
    internal virtual string InstallLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}";

    public event EventHandler<int>? DownloadProgressChanged;
    public event EventHandler<string>? DownloadComplete;
    public event EventHandler<int>? InstallProgressChanged;
    public event EventHandler<string>? InstallComplete;
    public event EventHandler<string>? ConsoleOutput;
    public event EventHandler<int>? Exited;
    public event EventHandler? StartupComplete;

    public void OnDownloadProgressChanged(int progress) => DownloadProgressChanged?.Invoke(this, progress);
    public void OnDownloadComplete(string path) => DownloadComplete?.Invoke(this, path);
    public void OnInstallProgressChanged(int progress) => InstallProgressChanged?.Invoke(this, progress);
    public void OnInstallComplete(string path) => InstallComplete?.Invoke(this, path);
    public void OnConsoleOutput(string output) => ConsoleOutput?.Invoke(this, output);
    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);
    public void OnStartupComplete() => StartupComplete?.Invoke(this, EventArgs.Empty);

    public string ByAuthor => $"By {Author}";
}
