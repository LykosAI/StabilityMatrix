using NLog;
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
    public override string LaunchCommand => 
        "-c \"__import__('invokeai.frontend.legacy_launch_invokeai').frontend.legacy_launch_invokeai.main()\"";

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
        await venvRunner.Setup().ConfigureAwait(false);
        
        
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        
        progress?.Report(new ProgressReport(1, "Installing Package", isIndeterminate: true));
        
        // If has Nvidia Gpu, install CUDA version
        if (gpus.Any(g => g.IsNvidia))
        {
            Logger.Info("Starting InvokeAI install (CUDA)...");
            await venvRunner.PipInstall(
                "InvokeAI[xformers] --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu117", 
                InstallLocation, OnConsoleOutput).ConfigureAwait(false);
        }
        // For AMD, Install ROCm version
        else if (gpus.Any(g => g.IsAmd))
        {
            Logger.Info("Starting InvokeAI install (ROCm)...");
            await venvRunner.PipInstall(
                "InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.4.2",
                InstallLocation, OnConsoleOutput).ConfigureAwait(false);
        }
        // For Apple silicon, use MPS
        else if (Compat.IsMacOS && Compat.IsArm)
        {
            Logger.Info("Starting InvokeAI install (MPS)...");
            await venvRunner.PipInstall(
                "InvokeAI --use-pep517",
                InstallLocation, OnConsoleOutput).ConfigureAwait(false);
        }
        // CPU Version
        else
        {
            Logger.Info("Starting InvokeAI install (CPU)...");
            await venvRunner.PipInstall(
                "pip install InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu",
                InstallLocation, OnConsoleOutput).ConfigureAwait(false);
        }
        
        await venvRunner
            .PipInstall("rich packaging python-dotenv", InstallLocation, OnConsoleOutput)
            .ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(1, "Installing Package", isIndeterminate: false));
    }
    
    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        var args = $"{LaunchCommand} {arguments}";
        
        VenvRunner?.RunDetached(
            args.TrimEnd(),
            outputDataReceived: OnConsoleOutput, 
            onExit: OnExit, 
            workingDirectory: installedPackagePath,
            environmentVariables: SettingsManager.Settings.EnvironmentVariables);
    }
}
