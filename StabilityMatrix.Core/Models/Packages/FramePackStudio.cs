using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, FramePackStudio>(Duplicate = DuplicateStrategy.Append)]
public class FramePackStudio(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : FramePack(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "framepack-studio";
    public override string DisplayName { get; set; } = "FramePack Studio";
    public override string Author => "colinurbs";
    public override string RepositoryName => "FramePack-Studio";
    public override string Blurb =>
        "FramePack Studio is an AI video generation application based on FramePack that strives to provide everything you need to create high quality video projects.";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/colinurbs/FramePack-Studio/blob/main/LICENSE";
    public override string LaunchCommand => "studio.py";
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.None, SharedFolderMethod.Configuration];

    public override SharedFolderLayout? SharedFolderLayout =>
        new()
        {
            ConfigFileType = ConfigFileType.Json,
            RelativeConfigPath = new FilePath(".framepack", "settings.json"),
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    ConfigDocumentPaths = ["lora_dir"],
                    TargetRelativePaths = ["loras"],
                    SourceTypes = [SharedFolderType.Lora],
                },
            ],
        };

    public override IReadOnlyDictionary<string, string> ExtraLaunchCommands =>
        new Dictionary<string, string>();
    public override IReadOnlyList<string> ExtraLaunchArguments => [];

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        if (installedPackage.PreferredSharedFolderMethod is SharedFolderMethod.Configuration)
        {
            var settingsPath = new FilePath(installLocation, ".framepack", "settings.json");
            if (!settingsPath.Exists)
            {
                settingsPath.Create();
            }

            // set the output_dir and metadata_dir
            var settingsText = await settingsPath.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);
            var json = JsonSerializer.Deserialize<JsonObject>(settingsText) ?? new JsonObject();
            json["output_dir"] = SettingsManager
                .ImagesDirectory.JoinDir(nameof(SharedOutputType.Img2Vid))
                .ToString();
            json["metadata_dir"] = SettingsManager
                .ImagesDirectory.JoinDir(nameof(SharedOutputType.Img2Vid))
                .ToString();

            await settingsPath
                .WriteAllTextAsync(
                    JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        VenvRunner.RunDetached(
            [LaunchCommand, .. options.Arguments, .. ExtraLaunchArguments],
            HandleConsoleOutput,
            OnExit
        );

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (match.Success)
                WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }
    }
}
