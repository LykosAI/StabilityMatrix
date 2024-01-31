using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Octokit;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper;

[SupportedOSPlatform("windows")]
[Obsolete("Not used in Avalonia, use WindowsPrerequisiteHelper instead")]
public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;
    private readonly IGitHubClient gitHubClient;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;

    private const string VcRedistDownloadUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";
    private const string PythonDownloadUrl =
        "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip";
    private const string PythonDownloadHashBlake3 =
        "24923775f2e07392063aaa0c78fbd4ae0a320e1fc9c6cfbab63803402279fe5a";

    private string HomeDir => settingsManager.LibraryDir;

    private string VcRedistDownloadPath => Path.Combine(HomeDir, "vcredist.x64.exe");

    private string AssetsDir => Path.Combine(HomeDir, "Assets");
    private string SevenZipPath => Path.Combine(AssetsDir, "7za.exe");

    private string PythonDownloadPath => Path.Combine(AssetsDir, "python-3.10.11-embed-amd64.zip");
    private string PythonDir => Path.Combine(AssetsDir, "Python310");
    private string PythonDllPath => Path.Combine(PythonDir, "python310.dll");
    private string PythonLibraryZipPath => Path.Combine(PythonDir, "python310.zip");
    private string GetPipPath => Path.Combine(PythonDir, "get-pip.pyc");

    // Temporary directory to extract venv to during python install
    private string VenvTempDir => Path.Combine(PythonDir, "venv");

    private string PortableGitInstallDir => Path.Combine(HomeDir, "PortableGit");
    private string PortableGitDownloadPath => Path.Combine(HomeDir, "PortableGit.7z.exe");
    private string GitExePath => Path.Combine(PortableGitInstallDir, "bin", "git.exe");
    public string GitBinPath => Path.Combine(PortableGitInstallDir, "bin");

    public bool IsPythonInstalled => File.Exists(PythonDllPath);

    public PrerequisiteHelper(
        ILogger<PrerequisiteHelper> logger,
        IGitHubClient gitHubClient,
        IDownloadService downloadService,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.gitHubClient = gitHubClient;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
    }

    public async Task RunGit(
        ProcessArgs args,
        Action<ProcessOutput>? onProcessOutput,
        string? workingDirectory = null
    )
    {
        var process = ProcessRunner.StartAnsiProcess(
            GitExePath,
            args.ToArray(),
            workingDirectory,
            onProcessOutput
        );
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new ProcessException($"Git exited with code {process.ExitCode}");
        }
    }

    public async Task RunGit(ProcessArgs args, string? workingDirectory = null)
    {
        var result = await ProcessRunner
            .GetProcessResultAsync(GitExePath, args, workingDirectory)
            .ConfigureAwait(false);

        result.EnsureSuccessExitCode();
    }

    /// <inheritdoc />
    public Task<ProcessResult> GetGitOutput(ProcessArgs args, string? workingDirectory = null)
    {
        throw new NotImplementedException();
    }

    public async Task<string> GetGitOutput(string? workingDirectory = null, params string[] args)
    {
        var output = await ProcessRunner
            .GetProcessOutputAsync(GitExePath, string.Join(" ", args), workingDirectory: workingDirectory)
            .ConfigureAwait(false);
        return output;
    }

    public Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }

    public Task RunNpm(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null
    )
    {
        throw new NotImplementedException();
    }

    public Task InstallNodeIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }

    public Task InstallPackageRequirements(BasePackage package, IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }

    public Task InstallPackageRequirements(
        List<PackagePrerequisite> prerequisites,
        IProgress<ProgressReport>? progress = null
    )
    {
        throw new NotImplementedException();
    }

    public Task InstallDotnetIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }

    public Task<Process> RunDotnet(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null,
        bool waitForExit = true
    )
    {
        throw new NotImplementedException();
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
        var fileName =
            resourceName
                .Replace(fileExt, "")
                .Replace("StabilityMatrix.Assets.", "")
                .Replace(".", Path.DirectorySeparatorChar.ToString()) + fileExt;
        await using var resourceStream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)!;
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
    private async Task ExtractAllEmbeddedResources(
        string resourceDir,
        string outputDirectory,
        string resourceRoot = "StabilityMatrix.Assets."
    )
    {
        Directory.CreateDirectory(outputDirectory);
        // Unpack from embedded resources
        var resources = GetEmbeddedResources().Where(r => r.StartsWith(resourceDir)).ToArray();
        var total = resources.Length;
        logger.LogInformation("Unpacking {Num} embedded resources...", total);

        // Unpack all resources
        var copied = new List<string>();
        foreach (var resourceName in resources)
        {
            // Convert resource name to file name
            // from "StabilityMatrix.Assets.Python310.libssl-1_1.dll"
            // to "Python310\libssl-1_1.dll"
            var fileExt = Path.GetExtension(resourceName);
            var fileName =
                resourceName
                    .Replace(fileExt, "")
                    .Replace(resourceRoot, "")
                    .Replace(".", Path.DirectorySeparatorChar.ToString()) + fileExt;
            // Unpack resource
            await using var resourceStream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(resourceName)!;
            var outputFilePath = Path.Combine(outputDirectory, fileName);
            // Create missing directories
            var outputDir = Path.GetDirectoryName(outputFilePath);
            if (outputDir != null)
            {
                Directory.CreateDirectory(outputDir);
            }
            await using var fileStream = File.Create(outputFilePath);
            await resourceStream.CopyToAsync(fileStream);
            copied.Add(outputFilePath);
        }
        logger.LogInformation(
            "Successfully unpacked {Num} embedded resources: [{Resources}]",
            total,
            string.Join(",", copied)
        );
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

        // Delete existing python zip if it exists
        if (File.Exists(PythonLibraryZipPath))
        {
            File.Delete(PythonLibraryZipPath);
        }

        logger.LogInformation(
            "Downloading Python from {PythonLibraryZipUrl} to {PythonLibraryZipPath}",
            PythonDownloadUrl,
            PythonLibraryZipPath
        );

        // Cleanup to remove zip if download fails
        try
        {
            // Download python zip
            await downloadService.DownloadToFileAsync(
                PythonDownloadUrl,
                PythonDownloadPath,
                progress: progress
            );

            // Verify python hash
            var downloadHash = await FileHash.GetBlake3Async(PythonDownloadPath, progress);
            if (downloadHash != PythonDownloadHashBlake3)
            {
                var fileExists = File.Exists(PythonDownloadPath);
                var fileSize = new FileInfo(PythonDownloadPath).Length;
                var msg =
                    $"Python download hash mismatch: {downloadHash} != {PythonDownloadHashBlake3} "
                    + $"(file exists: {fileExists}, size: {fileSize})";
                throw new Exception(msg);
            }

            progress?.Report(new ProgressReport(progress: 1f, message: "Python download complete"));

            progress?.Report(new ProgressReport(-1, "Installing Python...", isIndeterminate: true));

            // We also need 7z if it's not already unpacked
            if (!File.Exists(SevenZipPath))
            {
                await ExtractEmbeddedResource("StabilityMatrix.Assets.7za.exe", AssetsDir);
                await ExtractEmbeddedResource("StabilityMatrix.Assets.7za - LICENSE.txt", AssetsDir);
            }

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
        finally
        {
            // Always delete zip after download
            if (File.Exists(PythonDownloadPath))
            {
                File.Delete(PythonDownloadPath);
            }
        }
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (File.Exists(GitExePath))
        {
            logger.LogDebug("Git already installed at {GitExePath}", GitExePath);
            return;
        }

        logger.LogInformation("Git not found at {GitExePath}, downloading...", GitExePath);

        var portableGitUrl =
            "https://github.com/git-for-windows/git/releases/download/v2.41.0.windows.1/PortableGit-2.41.0-64-bit.7z.exe";

        if (!File.Exists(PortableGitDownloadPath))
        {
            await downloadService.DownloadToFileAsync(
                portableGitUrl,
                PortableGitDownloadPath,
                progress: progress
            );
            progress?.Report(new ProgressReport(progress: 1f, message: "Git download complete"));
        }

        await UnzipGit(progress);
    }

    [SupportedOSPlatform("windows")]
    public async Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64", false);
        if (key != null)
        {
            var buildId = Convert.ToUInt32(key.GetValue("Bld"));
            if (buildId >= 30139)
            {
                return;
            }
        }

        logger.LogInformation("Downloading VC Redist");

        await downloadService.DownloadToFileAsync(
            VcRedistDownloadUrl,
            VcRedistDownloadPath,
            progress: progress
        );
        progress?.Report(
            new ProgressReport(
                progress: 1f,
                message: "Visual C++ download complete",
                type: ProgressType.Download
            )
        );

        logger.LogInformation("Installing VC Redist");
        progress?.Report(
            new ProgressReport(
                progress: 0.5f,
                isIndeterminate: true,
                type: ProgressType.Generic,
                message: "Installing prerequisites..."
            )
        );
        var process = ProcessRunner.StartAnsiProcess(VcRedistDownloadPath, "/install /quiet /norestart");
        await process.WaitForExitAsync();
        progress?.Report(
            new ProgressReport(
                progress: 1f,
                message: "Visual C++ install complete",
                type: ProgressType.Generic
            )
        );

        File.Delete(VcRedistDownloadPath);
    }

    public void UpdatePathExtensions()
    {
        settingsManager.Settings.PathExtensions?.Clear();
        settingsManager.AddPathExtension(GitBinPath);
        settingsManager.InsertPathExtensions();
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
