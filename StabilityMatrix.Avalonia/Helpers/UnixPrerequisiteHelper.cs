using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("linux")]
public class UnixPrerequisiteHelper : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    
    private string HomeDir => settingsManager.LibraryDir;
    private string AssetsDir => Path.Combine(HomeDir, "Assets");
    
    private string PythonDir => Path.Combine(AssetsDir, "Python310");
    private string PythonDllPath => Path.Combine(PythonDir, "python310.dll");
    public bool IsPythonInstalled => File.Exists(PythonDllPath);
    
    private string PortableGitInstallDir => Path.Combine(HomeDir, "PortableGit");
    public string GitBinPath => Path.Combine(PortableGitInstallDir, "bin");
    

    public UnixPrerequisiteHelper(IDownloadService downloadService, ISettingsManager settingsManager)
    {
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
    }

    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await UnpackResourcesIfNecessary(progress);
        await InstallGitIfNecessary(progress);
        await InstallPythonIfNecessary(progress);
    }

    public async Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // Array of (asset_uri, extract_to)
        var assets = new[]
        {
            (Assets.SevenZipExecutable, AssetsDir),
            (Assets.SevenZipLicense, AssetsDir),
        };
        
        progress?.Report(new ProgressReport(0, message: "Unpacking resources", isIndeterminate: true));

        Directory.CreateDirectory(AssetsDir);
        foreach (var (assetUri, extractTo) in assets)
        {
            await Assets.ExtractAsset(assetUri, extractTo);
        }
        
        progress?.Report(new ProgressReport(1, message: "Unpacking resources", isIndeterminate: false));
    }

    public Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        return Task.CompletedTask;
    }
    
    public async Task RunGit(string? workingDirectory = null, params string[] args)
    {
        var result = await ProcessRunner.RunBashCommand("git" + args, workingDirectory ?? "");
        if (result.ExitCode != 0)
        {
            throw new ProcessException($"Git command failed with exit code {result.ExitCode}:\n" +
                                       $"{result.StandardOutput}\n{result.StandardError}");
        }
    }
    
    public async Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (IsPythonInstalled) return;

        Directory.CreateDirectory(AssetsDir);
            
        // Download
        var (url, hashSha256) = Assets.PythonDownloadUrl;
        var fileName = Path.GetFileName(url.LocalPath);
        var downloadPath = Path.Combine(AssetsDir, fileName);
        Logger.Info($"Downloading Python from {url.AbsolutePath} to {downloadPath}");
        try
        {
            await downloadService.DownloadToFileAsync(url.AbsolutePath, downloadPath, progress);
            
            // Verify hash
            var actualHash = await FileHash.GetSha256Async(downloadPath);
            Logger.Info($"Verifying Python hash: (expected: {hashSha256}, actual: {actualHash})");
            if (actualHash != hashSha256)
            {
                throw new Exception($"Python download hash mismatch: expected {hashSha256}, actual {actualHash}");
            }
            
            // Extract
            Logger.Info($"Extracting Python Zip: {downloadPath} to {PythonDir}");
            Directory.Delete(PythonDir, true);
            if (progress != null)
            {
                await ArchiveHelper.Extract7Z(downloadPath, PythonDir, progress);
            }
            else
            {
                await ArchiveHelper.Extract7Z(downloadPath, PythonDir);
            }
            
            // For Linux, move the inner 'python' folder up to the root PythonDir
            if (Compat.IsLinux)
            {
                var innerPythonDir = Path.Combine(PythonDir, "python");
                foreach (var folder in Directory.EnumerateDirectories(innerPythonDir))
                {
                    var folderName = Path.GetFileName(folder);
                    var dest = Path.Combine(PythonDir, folderName);
                    Directory.Move(folder, dest);
                }
                Directory.Delete(innerPythonDir);
            }
        }
        finally
        {
            // Cleanup download file
            File.Delete(downloadPath);
        }
    }
    
    public Task SetupPythonDependencies(string installLocation, string requirementsFileName,
        IProgress<ProgressReport>? progress = null, Action<ProcessOutput>? onConsoleOutput = null)
    {
        throw new NotImplementedException();
    }

    public void UpdatePathExtensions()
    {
        throw new NotImplementedException();
    }
    
    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }
}
