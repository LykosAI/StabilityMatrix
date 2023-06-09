﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Octokit;
using StabilityMatrix.Models;
using StabilityMatrix.Python;
using StabilityMatrix.Services;

namespace StabilityMatrix.Helper;

public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;
    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private const string VcRedistDownloadUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";
    private const string PythonDownloadUrl = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip";

    private static readonly string AppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string HomeDir = Path.Combine(AppDataDir, "StabilityMatrix");
    
    private static readonly string VcRedistDownloadPath = Path.Combine(HomeDir, "vcredist.x64.exe");

    private static readonly string AssetsDir = Path.Combine(HomeDir, "Assets");
    private static readonly string SevenZipPath = Path.Combine(AssetsDir, "7za.exe");
    
    private static readonly string PythonDownloadPath = Path.Combine(AssetsDir, "python-3.10.11-embed-amd64.zip");
    private static readonly string PythonDir = Path.Combine(AssetsDir, "Python310");
    private static readonly string PythonDllPath = Path.Combine(PythonDir, "python310.dll");
    private static readonly string PythonLibraryZipPath = Path.Combine(PythonDir, "python310.zip");
    private static readonly string GetPipPath = Path.Combine(PythonDir, "get-pip.pyc");
    // Temporary directory to extract venv to during python install
    private static readonly string VenvTempDir = Path.Combine(PythonDir, "venv");
    
    private static readonly string PortableGitInstallDir = Path.Combine(HomeDir, "PortableGit");
    private static readonly string PortableGitDownloadPath = Path.Combine(HomeDir, "PortableGit.7z.exe");
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
    
    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await InstallVcRedistIfNecessary(progress);
        await UnpackResourcesIfNecessary(progress);
        await InstallPythonIfNecessary(progress);
        await InstallGitIfNecessary(progress);
    }

    private static IEnumerable<string> GetEmbeddedResources()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceNames();
    }

    private async Task ExtractEmbeddedResource(string resourceName, string outputDirectory)
    {
        // Convert resource name to file name
        // from "StabilityMatrix.Assets.Python310.libssl-1_1.dll"
        // to "Python310\libssl-1_1.dll"
        var fileExt = Path.GetExtension(resourceName);
        var fileName = resourceName
            .Replace(fileExt, "")
            .Replace("StabilityMatrix.Assets.", "")
            .Replace(".", Path.DirectorySeparatorChar.ToString()) + fileExt;
        await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        if (resourceStream == null)
        {
            throw new Exception($"Resource {resourceName} not found");
        }
        await using var fileStream = File.Create(Path.Combine(outputDirectory, fileName));
        await resourceStream.CopyToAsync(fileStream);
    }

    /// <summary>
    /// Extracts all embedded resources starting with resourceDir to outputDirectory
    /// </summary>
    private async Task ExtractAllEmbeddedResources(string resourceDir, string outputDirectory, string resourceRoot = "StabilityMatrix.Assets.")
    {
        Directory.CreateDirectory(outputDirectory);
        // Unpack from embedded resources
        var resources = GetEmbeddedResources().Where(r => r.StartsWith(resourceDir)).ToArray();
        var total = resources.Length;
        logger.LogInformation("Unpacking {Num} embedded resources... [{Resources}]", total, string.Join(",", resources));

        // Unpack all resources
        foreach (var resourceName in resources)
        {
            // Convert resource name to file name
            // from "StabilityMatrix.Assets.Python310.libssl-1_1.dll"
            // to "Python310\libssl-1_1.dll"
            var fileExt = Path.GetExtension(resourceName);
            var fileName = resourceName
                .Replace(fileExt, "")
                .Replace(resourceRoot, "")
                .Replace(".", Path.DirectorySeparatorChar.ToString()) + fileExt;
            // Unpack resource
            await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
            var outputFilePath = Path.Combine(outputDirectory, fileName);
            // Create missing directories
            var outputDir = Path.GetDirectoryName(outputFilePath);
            if (outputDir != null)
            {
                Directory.CreateDirectory(outputDir);
            }
            await using var fileStream = File.Create(outputFilePath);
            await resourceStream.CopyToAsync(fileStream);
        }
    }

    public async Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        // Skip if all files exist
        if (File.Exists(SevenZipPath) && File.Exists(PythonDllPath) && File.Exists(PythonLibraryZipPath))
        {
            return;
        }
        // Start Progress
        progress?.Report(new ProgressReport(-1, "Unpacking resources...", isIndeterminate: true));
        // Create directories
        Directory.CreateDirectory(AssetsDir);
        Directory.CreateDirectory(PythonDir);
        
        // Run if 7za missing
        if (!File.Exists(SevenZipPath))
        {
            await ExtractEmbeddedResource("StabilityMatrix.Assets.7za.exe", AssetsDir);
            await ExtractEmbeddedResource("StabilityMatrix.Assets.7za - LICENSE.txt", AssetsDir);
        }
        
        progress?.Report(new ProgressReport(1f, "Unpacking complete"));
    }

    public async Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(PythonDllPath))
        {
            logger.LogDebug("Python already installed at {PythonDllPath}", PythonDllPath);
            return;
        }
        
        logger.LogInformation("Python not found at {PythonDllPath}, downloading...", PythonDllPath);
        
        Directory.CreateDirectory(AssetsDir);
        
        if (!File.Exists(PythonDownloadPath))
        {
            await downloadService.DownloadToFileAsync(PythonDownloadUrl, PythonDownloadPath, progress: progress);
            progress?.Report(new ProgressReport(progress: 1f, message: "Python download complete"));
        }
        
        progress?.Report(new ProgressReport(-1, "Installing Python...", isIndeterminate: true));
        
        // Delete existing python dir
        if (Directory.Exists(PythonDir))
        {
            Directory.Delete(PythonDir, true);
        }
        // Unzip python
        await ArchiveHelper.Extract7Z(PythonDownloadPath, PythonDir);
        
        try
        {
            // Extract embedded venv
            await ExtractAllEmbeddedResources("StabilityMatrix.Assets.venv", PythonDir);
            // Add venv to python's library zip
            await ArchiveHelper.AddToArchive7Z(PythonLibraryZipPath, VenvTempDir);
        }
        finally
        {
            // Remove venv
            if (Directory.Exists(VenvTempDir))
            {
                Directory.Delete(VenvTempDir, true);
            }
        }
        
        // Extract get-pip.pyc
        await ExtractEmbeddedResource("StabilityMatrix.Assets.get-pip.pyc", PythonDir);
        
        // We need to uncomment the #import site line in python310._pth for pip to work
        var pythonPthPath = Path.Combine(PythonDir, "python310._pth");
        var pythonPthContent = await File.ReadAllTextAsync(pythonPthPath);
        pythonPthContent = pythonPthContent.Replace("#import site", "import site");
        await File.WriteAllTextAsync(pythonPthPath, pythonPthContent);
        
        progress?.Report(new ProgressReport(1f, "Python install complete"));
    }

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
            .First(a => a.Name.EndsWith("64-bit.7z.exe")).BrowserDownloadUrl;

        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(portableGitUrl, PortableGitDownloadPath, progress: progress);
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);
    }

    public async Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64", false);
        if (key != null)
        {
            var buildId = Convert.ToUInt32(key.GetValue("Bld"));
            if (buildId >= 30139)
            {
                return;
            }
        }
        
        logger.LogInformation("Downloading VC Redist");

        await downloadService.DownloadToFileAsync(VcRedistDownloadUrl, VcRedistDownloadPath, progress: progress);
        progress?.Report(new ProgressReport(progress: 1f, message: "Visual C++ download complete",
            type: ProgressType.Download));
        
        logger.LogInformation("Installing VC Redist");
        progress?.Report(new ProgressReport(progress: 0.5f, isIndeterminate: true, type: ProgressType.Generic, message: "Installing prerequisites..."));
        var process = ProcessRunner.StartProcess(VcRedistDownloadPath, "/install /quiet /norestart");
        await process.WaitForExitAsync();
        progress?.Report(new ProgressReport(progress: 1f, message: "Visual C++ install complete",
            type: ProgressType.Generic));
        
        File.Delete(VcRedistDownloadPath);
    }

    public async Task SetupPythonDependencies(string installLocation, string requirementsFileName,
        IProgress<ProgressReport>? progress = null, Action<string?>? onConsoleOutput = null)
    {
        // Setup dependencies
        progress?.Report(new ProgressReport(-1, isIndeterminate: true));
        var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));

        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }

        void HandleConsoleOutput(string? s)
        {
            Debug.WriteLine($"venv stdout: {s}");
            onConsoleOutput?.Invoke(s);
        }

        // Install torch
        logger.LogDebug("Starting torch install...");
        await venvRunner.PipInstall(venvRunner.GetTorchInstallCommand(), installLocation, HandleConsoleOutput);

        // Install xformers if nvidia
        if (HardwareHelper.HasNvidiaGpu())
        {
            await venvRunner.PipInstall("xformers", installLocation, HandleConsoleOutput);
        }

        // Install requirements
        logger.LogDebug("Starting requirements install...");
        await venvRunner.PipInstall($"-r {requirementsFileName}", installLocation, HandleConsoleOutput);

        logger.LogDebug("Finished installing requirements!");
        progress?.Report(new ProgressReport(1, isIndeterminate: false));
    }

    private async Task UnzipGit(IProgress<ProgressReport>? progress = null)
    {
        if (progress == null)
        {
            await ArchiveHelper.Extract7Z(PortableGitDownloadPath, PortableGitInstallDir);
        }
        else
        {
            await ArchiveHelper.Extract7Z(PortableGitDownloadPath, PortableGitInstallDir, progress);
        }

        logger.LogInformation("Extracted Git");

        File.Delete(PortableGitDownloadPath);
        // Also add git to the path
        settingsManager.AddPathExtension(GitBinPath);
        settingsManager.InsertPathExtensions();
    }

}