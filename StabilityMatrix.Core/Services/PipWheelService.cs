using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using Octokit;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Service for installing pip wheel packages from GitHub releases.
/// </summary>
[RegisterSingleton<IPipWheelService, PipWheelService>]
public class PipWheelService(
    IGithubApiCache githubApi,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : IPipWheelService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    #region Triton

    /// <inheritdoc />
    public async Task InstallTritonAsync(
        IPyVenvRunner venv,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    )
    {
        // No-op on macOS
        if (Compat.IsMacOS)
        {
            Logger.Info("Skipping Triton installation - not supported on macOS");
            return;
        }

        var packageName = Compat.IsWindows ? "triton-windows" : "triton";
        var versionSpec = string.IsNullOrWhiteSpace(version) ? "" : $"=={version}";

        progress?.Report(new ProgressReport(-1f, $"Installing {packageName}", isIndeterminate: true));

        await venv.PipInstall($"{packageName}{versionSpec}", progress.AsProcessOutputHandler())
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Triton installed", isIndeterminate: false));
    }

    #endregion

    #region SageAttention

    /// <inheritdoc />
    public async Task InstallSageAttentionAsync(
        IPyVenvRunner venv,
        GpuInfo? gpuInfo = null,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    )
    {
        // No-op on macOS
        if (Compat.IsMacOS)
        {
            Logger.Info("Skipping SageAttention installation - not supported on macOS");
            return;
        }

        // No-op for non-NVIDIA GPUs (SageAttention requires CUDA)
        if (gpuInfo is not null && !gpuInfo.IsNvidia)
        {
            Logger.Info("Skipping SageAttention installation - requires NVIDIA GPU");
            return;
        }

        // On Linux, can use pip directly
        if (Compat.IsLinux)
        {
            var versionSpec = string.IsNullOrWhiteSpace(version) ? "" : $"=={version}";
            progress?.Report(new ProgressReport(-1f, "Installing SageAttention", isIndeterminate: true));
            await venv.PipInstall($"sageattention{versionSpec}", progress.AsProcessOutputHandler())
                .ConfigureAwait(false);
            progress?.Report(new ProgressReport(1f, "SageAttention installed", isIndeterminate: false));
            return;
        }

        // Windows: find wheel from GitHub releases
        await InstallSageAttentionWindowsAsync(venv, gpuInfo, progress, version).ConfigureAwait(false);
    }

    private async Task InstallSageAttentionWindowsAsync(
        IPyVenvRunner venv,
        GpuInfo? gpuInfo,
        IProgress<ProgressReport>? progress,
        string? version
    )
    {
        var torchInfo = await venv.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            Logger.Warn("Cannot install SageAttention - torch not installed");
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Finding SageAttention wheel", isIndeterminate: true));

        // Get releases from GitHub
        var releases = await githubApi.GetAllReleases("woct0rdho", "SageAttention").ConfigureAwait(false);
        var releaseList = releases
            .Where(r => r.TagName.Contains("windows"))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        if (releaseList.Count == 0)
        {
            Logger.Warn("No SageAttention Windows releases found");
            await InstallSageAttentionFromSourceAsync(venv, progress).ConfigureAwait(false);
            return;
        }

        // Find matching wheel from release assets
        var wheelUrl = FindMatchingWheelAsset(releaseList, torchInfo, venv.Version, version);

        if (!string.IsNullOrWhiteSpace(wheelUrl))
        {
            progress?.Report(
                new ProgressReport(-1f, "Installing Triton & SageAttention", isIndeterminate: true)
            );

            // Install triton-windows first, then sage with --no-deps to prevent torch reinstall
            var pipArgs = new PipInstallArgs("triton-windows").AddArg("--no-deps").AddArg(wheelUrl);
            await venv.PipInstall(pipArgs, progress.AsProcessOutputHandler()).ConfigureAwait(false);

            progress?.Report(new ProgressReport(1f, "SageAttention installed", isIndeterminate: false));
            return;
        }

        // No wheel found - fall back to building from source
        Logger.Info("No matching SageAttention wheel found, building from source");
        await InstallSageAttentionFromSourceAsync(venv, progress).ConfigureAwait(false);
    }

    private static string? FindMatchingWheelAsset(
        IEnumerable<Release> releases,
        PipShowResult torchInfo,
        PyVersion pyVersion,
        string? targetVersion
    )
    {
        // Parse torch info
        var torchVersionStr = torchInfo.Version;
        var plusIndex = torchVersionStr.IndexOf('+');
        var baseTorchVersion = plusIndex >= 0 ? torchVersionStr[..plusIndex] : torchVersionStr;
        var cudaIndex = plusIndex >= 0 ? torchVersionStr[(plusIndex + 1)..] : "";

        // Get major.minor of torch
        var torchParts = baseTorchVersion.Split('.');
        var shortTorch = torchParts.Length >= 2 ? $"{torchParts[0]}.{torchParts[1]}" : baseTorchVersion;

        // Get python version string (e.g., "cp312")
        var shortPy = $"cp3{pyVersion.Minor}";

        foreach (var release in releases)
        {
            // If a specific version is requested, filter releases
            if (!string.IsNullOrWhiteSpace(targetVersion) && !release.TagName.Contains(targetVersion))
                continue;

            foreach (var asset in release.Assets)
            {
                var name = asset.Name;

                // Must be a wheel file
                if (!name.EndsWith(".whl"))
                    continue;

                // Must be for Windows
                if (!name.Contains("win_amd64"))
                    continue;

                // Check Python version compatibility (cp39-abi3 works for cp39+, or specific version)
                var matchesPython =
                    name.Contains($"{shortPy}-{shortPy}")
                    || name.Contains("cp39-abi3")
                    || (pyVersion.Minor >= 9 && name.Contains("abi3"));

                if (!matchesPython)
                    continue;

                // Check torch version match
                // Assets use patterns like: cu128torch2.9.0 or cu130torch2.9.0andhigher
                var matchesTorch =
                    name.Contains($"torch{shortTorch}")
                    || name.Contains($"torch{baseTorchVersion}")
                    || (name.Contains("andhigher") && CompareTorchVersions(baseTorchVersion, name));

                // Check CUDA index match
                var matchesCuda = !string.IsNullOrEmpty(cudaIndex) && name.Contains(cudaIndex);

                if (matchesTorch && matchesCuda)
                {
                    Logger.Info("Found matching SageAttention wheel: {Name}", name);
                    return asset.BrowserDownloadUrl;
                }
            }
        }

        return null;
    }

    private static bool CompareTorchVersions(string installedTorch, string assetName)
    {
        // Extract torch version from asset name (e.g., "torch2.9.0andhigher" -> "2.9.0")
        var match = Regex.Match(assetName, @"torch(\d+\.\d+\.\d+)");
        if (!match.Success)
            return false;

        if (!Version.TryParse(installedTorch, out var installed))
            return false;

        if (!Version.TryParse(match.Groups[1].Value, out var required))
            return false;

        // "andhigher" means installed version must be >= required version
        return installed >= required;
    }

    private async Task InstallSageAttentionFromSourceAsync(
        IPyVenvRunner venv,
        IProgress<ProgressReport>? progress
    )
    {
        // Check prerequisites
        if (!prerequisiteHelper.IsVcBuildToolsInstalled)
        {
            Logger.Warn("Cannot build SageAttention from source - VS Build Tools not installed");
            return;
        }

        var nvccPath = await Utilities.WhichAsync("nvcc").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(nvccPath))
        {
            var cuda126Path = new DirectoryPath(
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6\bin"
            );
            var cuda128Path = new DirectoryPath(
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8\bin"
            );

            if (!cuda126Path.Exists && !cuda128Path.Exists)
            {
                Logger.Warn("Cannot build SageAttention from source - CUDA Toolkit not found");
                return;
            }

            nvccPath = cuda128Path.Exists
                ? cuda128Path.JoinFile("nvcc.exe").ToString()
                : cuda126Path.JoinFile("nvcc.exe").ToString();
        }

        // Set up CUDA environment
        var cudaBinPath = Path.GetDirectoryName(nvccPath)!;
        var cudaHome = Path.GetDirectoryName(cudaBinPath)!;

        venv.UpdateEnvironmentVariables(env =>
        {
            env = env.TryGetValue("PATH", out var pathValue)
                ? env.SetItem("PATH", $"{cudaBinPath}{Path.PathSeparator}{pathValue}")
                : env.Add("PATH", cudaBinPath);

            if (!env.ContainsKey("CUDA_HOME"))
            {
                env = env.Add("CUDA_HOME", cudaHome);
            }

            return env;
        });

        progress?.Report(new ProgressReport(-1f, "Installing Triton", isIndeterminate: true));
        await venv.PipInstall("triton-windows", progress.AsProcessOutputHandler()).ConfigureAwait(false);

        venv.UpdateEnvironmentVariables(env => env.SetItem("SETUPTOOLS_USE_DISTUTILS", "setuptools"));

        // Download python libs for building
        await AddMissingLibsToVenvAsync(venv, progress).ConfigureAwait(false);

        var sageDir = venv.WorkingDirectory?.JoinDir("SageAttention") ?? new DirectoryPath("SageAttention");

        if (!sageDir.Exists)
        {
            progress?.Report(new ProgressReport(-1f, "Downloading SageAttention", isIndeterminate: true));
            await prerequisiteHelper
                .RunGit(
                    ["clone", "https://github.com/thu-ml/SageAttention.git", sageDir.ToString()],
                    progress.AsProcessOutputHandler()
                )
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1f, "Building SageAttention", isIndeterminate: true));
        await venv.PipInstall([sageDir.ToString()], progress.AsProcessOutputHandler()).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "SageAttention built and installed", isIndeterminate: false));
    }

    private async Task AddMissingLibsToVenvAsync(IPyVenvRunner venv, IProgress<ProgressReport>? progress)
    {
        var venvLibsDir = venv.RootPath.JoinDir("libs");
        var venvIncludeDir = venv.RootPath.JoinDir("include");

        if (
            venvLibsDir.Exists
            && venvIncludeDir.Exists
            && venvLibsDir.JoinFile("python3.lib").Exists
            && venvLibsDir.JoinFile("python310.lib").Exists
        )
        {
            return;
        }

        const string pythonLibsUrl = "https://cdn.lykos.ai/python_libs_for_sage.zip";
        var downloadPath = venv.RootPath.JoinFile("python_libs_for_sage.zip");

        progress?.Report(new ProgressReport(-1f, "Downloading Python libraries", isIndeterminate: true));
        await downloadService
            .DownloadToFileAsync(pythonLibsUrl, downloadPath, progress)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Extracting Python libraries", isIndeterminate: true));
        await ArchiveHelper.Extract7Z(downloadPath, venv.RootPath, progress).ConfigureAwait(false);

        var includeFolder = venv.RootPath.JoinDir("include");
        var scriptsIncludeFolder = venv.RootPath.JoinDir("Scripts", "include");
        await includeFolder.CopyToAsync(scriptsIncludeFolder).ConfigureAwait(false);

        await downloadPath.DeleteAsync().ConfigureAwait(false);
    }

    #endregion

    #region Nunchaku

    /// <inheritdoc />
    public async Task InstallNunchakuAsync(
        IPyVenvRunner venv,
        GpuInfo? gpuInfo = null,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    )
    {
        // No-op on macOS
        if (Compat.IsMacOS)
        {
            Logger.Info("Skipping Nunchaku installation - not supported on macOS");
            return;
        }

        // No-op for GPUs with compute capability < 7.5
        if (gpuInfo?.ComputeCapabilityValue is < 7.5m)
        {
            Logger.Info("Skipping Nunchaku installation - GPU compute capability < 7.5");
            return;
        }

        var torchInfo = await venv.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            Logger.Warn("Cannot install Nunchaku - torch not installed");
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Finding Nunchaku wheel", isIndeterminate: true));

        // Get releases from GitHub
        var releases = await githubApi.GetAllReleases("nunchaku-ai", "nunchaku").ConfigureAwait(false);
        var releaseList = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.CreatedAt).ToList();

        if (releaseList.Count == 0)
        {
            Logger.Warn("No Nunchaku releases found");
            return;
        }

        var wheelUrl = FindMatchingNunchakuWheelAsset(releaseList, torchInfo, venv.Version, version);

        if (string.IsNullOrWhiteSpace(wheelUrl))
        {
            Logger.Warn("No compatible Nunchaku wheel found for torch {TorchVersion}", torchInfo.Version);
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Installing Nunchaku", isIndeterminate: true));
        // Use --no-deps to prevent reinstalling torch without CUDA
        await venv.PipInstall(
                new PipInstallArgs("--no-deps").AddArg(wheelUrl),
                progress.AsProcessOutputHandler()
            )
            .ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Nunchaku installed", isIndeterminate: false));
    }

    private static string? FindMatchingNunchakuWheelAsset(
        IEnumerable<Release> releases,
        PipShowResult torchInfo,
        PyVersion pyVersion,
        string? targetVersion
    )
    {
        // Parse torch version
        var torchVersionStr = torchInfo.Version;
        var plusIndex = torchVersionStr.IndexOf('+');
        var baseTorchVersion = plusIndex >= 0 ? torchVersionStr[..plusIndex] : torchVersionStr;
        var torchParts = baseTorchVersion.Split('.');
        var shortTorch = torchParts.Length >= 2 ? $"{torchParts[0]}.{torchParts[1]}" : baseTorchVersion;

        // Get python version string
        var shortPy = $"cp3{pyVersion.Minor}";

        // Get platform
        var platform = Compat.IsWindows ? "win_amd64" : "linux_x86_64";

        Logger.Debug(
            "Searching for Nunchaku wheel: Python={ShortPy}, Torch={ShortTorch}, Platform={Platform}",
            shortPy,
            shortTorch,
            platform
        );

        foreach (var release in releases)
        {
            // If a specific version is requested, filter releases
            if (!string.IsNullOrWhiteSpace(targetVersion) && !release.TagName.Contains(targetVersion))
                continue;

            foreach (var asset in release.Assets)
            {
                var name = asset.Name;

                if (!name.EndsWith(".whl"))
                    continue;

                if (!name.Contains(platform))
                    continue;

                // Check Python version
                if (!name.Contains($"{shortPy}-{shortPy}"))
                    continue;

                // Check torch version (assets use patterns like: torch2.7 or torch2.8)
                if (!name.Contains($"torch{shortTorch}"))
                    continue;

                Logger.Info(
                    "Found matching Nunchaku wheel: {Name} (Python={ShortPy}, Torch={ShortTorch})",
                    name,
                    shortPy,
                    shortTorch
                );
                return asset.BrowserDownloadUrl;
            }
        }

        return null;
    }

    #endregion

    #region FlashAttention

    /// <inheritdoc />
    public async Task InstallFlashAttentionAsync(
        IPyVenvRunner venv,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    )
    {
        // Windows only
        if (!Compat.IsWindows)
        {
            Logger.Info("Skipping FlashAttention installation - Windows only");
            return;
        }

        var torchInfo = await venv.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            Logger.Warn("Cannot install FlashAttention - torch not installed");
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Finding FlashAttention wheel", isIndeterminate: true));

        // Get releases from GitHub
        var releases = await githubApi
            .GetAllReleases("mjun0812", "flash-attention-prebuild-wheels")
            .ConfigureAwait(false);
        var releaseList = releases.OrderByDescending(r => r.CreatedAt).ToList();

        if (releaseList.Count == 0)
        {
            Logger.Warn("No FlashAttention releases found");
            return;
        }

        var wheelUrl = FindMatchingFlashAttentionWheelAsset(releaseList, torchInfo, venv.Version, version);

        if (string.IsNullOrWhiteSpace(wheelUrl))
        {
            Logger.Warn(
                "No compatible FlashAttention wheel found for torch {TorchVersion}",
                torchInfo.Version
            );
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Installing FlashAttention", isIndeterminate: true));
        // Use --no-deps to prevent reinstalling torch without CUDA
        await venv.PipInstall(
                new PipInstallArgs("--no-deps").AddArg(wheelUrl),
                progress.AsProcessOutputHandler()
            )
            .ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "FlashAttention installed", isIndeterminate: false));
    }

    private static string? FindMatchingFlashAttentionWheelAsset(
        IEnumerable<Release> releases,
        PipShowResult torchInfo,
        PyVersion pyVersion,
        string? targetVersion
    )
    {
        // Parse torch version and CUDA index
        var torchVersionStr = torchInfo.Version;
        var plusIndex = torchVersionStr.IndexOf('+');
        var baseTorchVersion = plusIndex >= 0 ? torchVersionStr[..plusIndex] : torchVersionStr;
        var cudaIndex = plusIndex >= 0 ? torchVersionStr[(plusIndex + 1)..] : "";
        var torchParts = baseTorchVersion.Split('.');
        var shortTorch = torchParts.Length >= 2 ? $"{torchParts[0]}.{torchParts[1]}" : baseTorchVersion;

        // Get python version string
        var shortPy = $"cp3{pyVersion.Minor}";

        foreach (var release in releases)
        {
            foreach (var asset in release.Assets)
            {
                var name = asset.Name;

                if (!name.EndsWith(".whl"))
                    continue;

                if (!name.Contains("win_amd64"))
                    continue;

                // Check for specific version if requested
                if (
                    !string.IsNullOrWhiteSpace(targetVersion) && !name.Contains($"flash_attn-{targetVersion}")
                )
                    continue;

                // Check Python version
                if (!name.Contains($"{shortPy}-{shortPy}"))
                    continue;

                // Check torch version
                if (!name.Contains($"torch{shortTorch}"))
                    continue;

                // Check CUDA index
                if (!string.IsNullOrEmpty(cudaIndex) && !name.Contains(cudaIndex))
                    continue;

                Logger.Info("Found matching FlashAttention wheel: {Name}", name);
                return asset.BrowserDownloadUrl;
            }
        }

        return null;
    }

    #endregion
}
