using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class InvokeAI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public override string Name => "InvokeAI";
    public override string DisplayName { get; set; } = "InvokeAI";
    public override string Author => "invoke-ai";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => 
        "https://github.com/invoke-ai/InvokeAI/blob/main/LICENSE";
    public override string Blurb => "Professional Creative Tools for Stable Diffusion";
    public override string LaunchCommand => "invokeai-web";

    public override IReadOnlyList<string> ExtraLaunchCommands => new[]
    {
        "invokeai-configure",
        "invokeai-merge",
        "invokeai-metadata",
        "invokeai-model-install",
        "invokeai-node-cli",
        "invokeai-ti",
        "invokeai-update",
    };

    public override Uri PreviewImageUri => new(
        "https://raw.githubusercontent.com/invoke-ai/InvokeAI/main/docs/assets/canvas_preview.png");
    
    public override bool ShouldIgnoreReleases => true;
    
    public InvokeAI(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) : 
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }
    
    // https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/main.py#L86
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
    };
    
    public override List<LaunchOptionDefinition> LaunchOptions => new List<LaunchOptionDefinition>
    {
        LaunchOptionDefinition.Extras
    };
    
    public override Task<string> GetLatestVersion() => Task.FromResult("main");
    
    public override Task<string> DownloadPackage(string version, bool isCommitHash,
        IProgress<ProgressReport>? progress = null)
    {
        return Task.FromResult(version);
    }
    
    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        // Setup venv
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        
        await using var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        venvRunner.WorkingDirectory = InstallLocation;
        await venvRunner.Setup().ConfigureAwait(false);
        
        
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        
        progress?.Report(new ProgressReport(1, "Installing Package", isIndeterminate: true));
        
        // If has Nvidia Gpu, install CUDA version
        if (gpus.Any(g => g.IsNvidia))
        {
            Logger.Info("Starting InvokeAI install (CUDA)...");
            await venvRunner.PipInstall(
                "InvokeAI[xformers] --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu117", 
                OnConsoleOutput).ConfigureAwait(false);
        }
        // For AMD, Install ROCm version
        else if (gpus.Any(g => g.IsAmd))
        {
            Logger.Info("Starting InvokeAI install (ROCm)...");
            await venvRunner.PipInstall(
                "InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.4.2",
                OnConsoleOutput).ConfigureAwait(false);
        }
        // For Apple silicon, use MPS
        else if (Compat.IsMacOS && Compat.IsArm)
        {
            Logger.Info("Starting InvokeAI install (MPS)...");
            await venvRunner.PipInstall(
                "InvokeAI --use-pep517",
                OnConsoleOutput).ConfigureAwait(false);
        }
        // CPU Version
        else
        {
            Logger.Info("Starting InvokeAI install (CPU)...");
            await venvRunner.PipInstall(
                "pip install InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu",
                OnConsoleOutput).ConfigureAwait(false);
        }
        
        await venvRunner
            .PipInstall("rich packaging python-dotenv", OnConsoleOutput)
            .ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(1, "Installing Package", isIndeterminate: false));
    }
    
    public override async Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        // Launch command is for a console entry point, and not a direct script
        var entryPoint = await VenvRunner.GetEntryPoint(command).ConfigureAwait(false);
        
        // Split at ':' to get package and function
        var split = entryPoint?.Split(':');
        
        if (split is not {Length: > 1})
        {
            throw new Exception($"Could not find entry point for InvokeAI: {entryPoint.ToRepr()}");
        }
        
        // Compile a startup command according to 
        // https://packaging.python.org/en/latest/specifications/entry-points/#use-for-scripts
        // For invokeai, also patch the shutil.get_terminal_size function to return a fixed value
        // above the minimum in invokeai.frontend.install.widgets
        
        var code = $"""
                   try:
                       import os
                       import shutil
                       from invokeai.frontend.install import widgets
                       
                       _min_cols = widgets.MIN_COLS
                       _min_lines = widgets.MIN_LINES
                       
                       static_size_fn = lambda: os.terminal_size((_min_cols, _min_lines))
                       shutil.get_terminal_size = static_size_fn
                       widgets.get_terminal_size = static_size_fn
                   except Exception as e:
                       import warnings
                       warnings.warn('Could not patch terminal size for InvokeAI' + str(e))
                   
                   import sys
                   from {split[0]} import {split[1]}
                   sys.exit({split[1]}())
                   """;
        
        VenvRunner.RunDetached($"-c \"{code}\"".TrimEnd(), OnConsoleOutput, OnExit);
    }
}
