using System;
using System.Threading.Tasks;

namespace StabilityMatrix.Models;

public abstract class BasePackage
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string Author { get; }
    public abstract string GithubUrl { get; }
    public abstract Task DownloadPackage();
    
    internal virtual string DownloadLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}.zip";
    internal virtual string InstallLocation => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages\\{Name}";

    public event EventHandler<int> DownloadProgressChanged;
    public event EventHandler<string> DownloadComplete;
    
    public void OnDownloadProgressChanged(int progress) => DownloadProgressChanged?.Invoke(this, progress);
    public void OnDownloadComplete(string path) => DownloadComplete?.Invoke(this, path);
    
    public string ByAuthor => $"By {Author}";
}
