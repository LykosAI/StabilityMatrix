using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("macos")]
[SupportedOSPlatform("linux")]
public class UnixPrerequisiteHelper : IPrerequisiteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly IPyRunner pyRunner;

    private DirectoryPath HomeDir => settingsManager.LibraryDir;
    private DirectoryPath AssetsDir => HomeDir.JoinDir("Assets");

    private DirectoryPath PythonDir => AssetsDir.JoinDir("Python310");
    public bool IsPythonInstalled => PythonDir.JoinFile(PyRunner.RelativePythonDllPath).Exists;

    private DirectoryPath PortableGitInstallDir => HomeDir + "PortableGit";
    public string GitBinPath => PortableGitInstallDir + "bin";

    // Cached store of whether or not git is installed
    private bool? isGitInstalled;

    public UnixPrerequisiteHelper(
        IDownloadService downloadService,
        ISettingsManager settingsManager,
        IPyRunner pyRunner
    )
    {
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.pyRunner = pyRunner;
    }

    private async Task<bool> CheckIsGitInstalled()
    {
        var result = await ProcessRunner.RunBashCommand("git --version");
        isGitInstalled = result.ExitCode == 0;
        return isGitInstalled == true;
    }

    public async Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        await UnpackResourcesIfNecessary(progress);
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

        progress?.Report(
            new ProgressReport(0, message: "Unpacking resources", isIndeterminate: true)
        );

        Directory.CreateDirectory(AssetsDir);
        foreach (var (asset, extractDir) in assets)
        {
            await asset.ExtractToDir(extractDir);
        }

        progress?.Report(
            new ProgressReport(1, message: "Unpacking resources", isIndeterminate: false)
        );
    }

    public async Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (isGitInstalled == true || (isGitInstalled == null && await CheckIsGitInstalled()))
            return;

        // Show prompt to install git
        var dialog = new ContentDialog
        {
            Title = "Git not found",
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "The current operation requires Git. Please install it to continue."
                    },
                    new SelectableTextBlock { Text = "$ sudo apt install git" },
                }
            },
            PrimaryButtonText = Resources.Action_Retry,
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Primary,
        };

        while (true)
        {
            // Return if installed
            if (await CheckIsGitInstalled())
                return;
            if (await dialog.ShowAsync() == ContentDialogResult.None)
            {
                // Cancel
                throw new OperationCanceledException("Git installation canceled");
            }
            // Otherwise continue to retry indefinitely
        }
    }

    public async Task RunGit(
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        params string[] args
    )
    {
        var command =
            args.Length == 0 ? "git" : "git " + string.Join(" ", args.Select(ProcessRunner.Quote));

        var result = await ProcessRunner.RunBashCommand(command, workingDirectory ?? "");
        if (result.ExitCode != 0)
        {
            Logger.Error(
                "Git command [{Command}] failed with exit code "
                    + "{ExitCode}:\n{StdOut}\n{StdErr}",
                command,
                result.ExitCode,
                result.StandardOutput,
                result.StandardError
            );

            throw new ProcessException(
                $"Git command [{command}] failed with exit code"
                    + $" {result.ExitCode}:\n{result.StandardOutput}\n{result.StandardError}"
            );
        }
    }

    public async Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        if (IsPythonInstalled)
            return;

        Directory.CreateDirectory(AssetsDir);

        // Download
        var remote = Assets.PythonDownloadUrl;
        var url = remote.Url;
        var hashSha256 = remote.HashSha256;

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
                throw new Exception(
                    $"Python download hash mismatch: expected {hashSha256}, actual {actualHash}"
                );
            }

            // Extract
            Logger.Info($"Extracting Python Zip: {downloadPath} to {PythonDir}");
            if (PythonDir.Exists)
            {
                await PythonDir.DeleteAsync(true);
            }
            progress?.Report(new ProgressReport(0, "Installing Python", isIndeterminate: true));
            await ArchiveHelper.Extract7ZAuto(downloadPath, PythonDir);

            // For Unix, move the inner 'python' folder up to the root PythonDir
            if (Compat.IsUnix)
            {
                var innerPythonDir = PythonDir.JoinDir("python");
                if (!innerPythonDir.Exists)
                {
                    throw new Exception(
                        $"Python download did not contain expected inner 'python' folder: {innerPythonDir}"
                    );
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

    public Task<string> GetGitOutput(string? workingDirectory = null, params string[] args)
    {
        throw new NotImplementedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("Linux")]
    [UnsupportedOSPlatform("macOS")]
    public Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null)
    {
        throw new PlatformNotSupportedException();
    }
}
