using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Utilities;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("linux")]
public class UnixPrerequisiteHelper : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly IPyRunner pyRunner;
    
    private DirectoryPath HomeDir => settingsManager.LibraryDir;
    private DirectoryPath AssetsDir => HomeDir + "Assets";

    private DirectoryPath PythonDir => AssetsDir + "Python310";
    private FilePath PythonDllPath => PythonDir + "python310.dll";
    public bool IsPythonInstalled => PythonDllPath.Exists;
    
    private DirectoryPath PortableGitInstallDir => HomeDir + "PortableGit";
    public string GitBinPath => PortableGitInstallDir + "bin";
    

    public UnixPrerequisiteHelper(
        IDownloadService downloadService, 
        ISettingsManager settingsManager,
        IPyRunner pyRunner)
    {
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.pyRunner = pyRunner;
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
        foreach (var (asset, extractDir) in assets)
        {
            await asset.ExtractTo(extractDir);
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
        Logger.Info($"Downloading Python from {url} to {downloadPath}");
        try
        {
            await downloadService.DownloadToFileAsync(url.ToString(), downloadPath, progress);
            
            // Verify hash
            var actualHash = await FileHash.GetSha256Async(downloadPath);
            Logger.Info($"Verifying Python hash: (expected: {hashSha256}, actual: {actualHash})");
            if (actualHash != hashSha256)
            {
                throw new Exception($"Python download hash mismatch: expected {hashSha256}, actual {actualHash}");
            }
            
            // Extract
            Logger.Info($"Extracting Python Zip: {downloadPath} to {PythonDir}");
            if (PythonDir.Exists)
            {
                await PythonDir.DeleteAsync(true);
            }
            progress?.Report(new ProgressReport(0, "Installing Python", isIndeterminate: true));
            await ArchiveHelper.Extract7ZAuto(downloadPath, PythonDir);
            
            // For Linux, move the inner 'python' folder up to the root PythonDir
            if (Compat.IsLinux)
            {
                var innerPythonDir = PythonDir.JoinDir("python");
                if (!innerPythonDir.Exists)
                {
                    throw new Exception($"Python download did not contain expected inner 'python' folder: {innerPythonDir}");
                }
                
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
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }
        }
        
        // Initialize pyrunner and install virtualenv
        await pyRunner.Initialize();
        await pyRunner.InstallPackage("virtualenv");
        
        progress?.Report(new ProgressReport(1, "Installing Python", isIndeterminate: false));
    }
    
    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }
}
