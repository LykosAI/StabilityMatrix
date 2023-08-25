using System.Diagnostics;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class Fooocus : BaseGitPackage
{
    public Fooocus(IGithubApiCache githubApi, ISettingsManager settingsManager,
        IDownloadService downloadService, IPrerequisiteHelper prerequisiteHelper) : base(githubApi,
        settingsManager, downloadService, prerequisiteHelper)
    {
    }

    public override string Name => "Fooocus";
    public override string DisplayName { get; set; } = "Fooocus";
    public override string Author => "lllyasviel";

    public override string Blurb =>
        "Fooocus is a rethinking of Stable Diffusion and Midjourney’s designs";

    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => "https://github.com/lllyasviel/Fooocus/blob/main/LICENSE";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new("https://user-images.githubusercontent.com/19834515/261830306-f79c5981-cf80-4ee3-b06b-3fef3f8bfbc7.png");

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        LaunchOptionDefinition.Extras
    };

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = new[] {"models/checkpoints"},
        [SharedFolderType.Diffusers] = new[] {"models/diffusers"},
        [SharedFolderType.Lora] = new[] {"models/loras"},
        [SharedFolderType.CLIP] = new[] {"models/clip"},
        [SharedFolderType.TextualInversion] = new[] {"models/embeddings"},
        [SharedFolderType.VAE] = new[] {"models/vae"},
        [SharedFolderType.ApproxVAE] = new[] {"models/vae_approx"},
        [SharedFolderType.ControlNet] = new[] {"models/controlnet"},
        [SharedFolderType.GLIGEN] = new[] {"models/gligen"},
        [SharedFolderType.ESRGAN] = new[] {"models/upscale_models"},
        [SharedFolderType.Hypernetwork] = new[] {"models/hypernetworks"}
    };
    
    public override async Task<string> GetLatestVersion()
    {
        var release = await GetLatestRelease().ConfigureAwait(false);
        return release.TagName!;
    }

    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await base.InstallPackage(progress).ConfigureAwait(false);
        var venvRunner = await SetupVenv(InstallLocation).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing torch...", isIndeterminate: true));
        
        var torchVersion = "cpu";
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        
        if (gpus.Any(g => g.IsNvidia))
        {
            torchVersion = "cu118";
        }
        else if (HardwareHelper.PreferRocm())
        {
            torchVersion = "rocm5.4.2";
        }

        await venvRunner
            .PipInstall(
                $"torch==2.0.1 torchvision==0.15.2 --extra-index-url https://download.pytorch.org/whl/{torchVersion}",
                OnConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...",
            isIndeterminate: true));
        await venvRunner.PipInstall("-r requirements_versions.txt", OnConsoleOutput)
            .ConfigureAwait(false);
    }

    public override async Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        void HandleConsoleOutput(ProcessOutput s)
        {
            OnConsoleOutput(s);
            
            if (s.Text.Contains("Use the app with", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
            }
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner?.RunDetached(
            args.TrimEnd(), 
            HandleConsoleOutput, 
            HandleExit);
    }
}
