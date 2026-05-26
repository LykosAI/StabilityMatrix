using System.Text.Json;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Provides the shared ROCm helper surface area used by ROCm-capable packages.
/// </summary>
[RegisterSingleton<IRocmPackageHelper, RocmPackageHelper>]
public class RocmPackageHelper(ISettingsManager settingsManager) : IRocmPackageHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly StringComparer EnvComparer = StringComparer.OrdinalIgnoreCase;
    private const string RocmSdkDevelPackageName = "rocm-sdk-devel";
    private static readonly string[] WindowsLaunchNoticeLines =
    [
        "Stability Matrix Windows ROCm Notice: Windows AMD ROCm support is experimental. Please report any issues to Stability Matrix first so it can be determined whether the issue is package-specific.",
        "Because this setup may not be officially supported by package developers, only contact upstream support for issues clearly caused by the package itself.",
    ];

    /// <summary>
    /// Evaluates the current Windows machine state for the given package profile and returns the resolved ROCm compatibility result.
    /// </summary>
    public RocmCompatibilityResult GetCompatibility(RocmPackageProfile profile)
    {
        return BuildCompatibilityResult();
    }

    /// <summary>
    /// Resolves launch-time ROCm runtime details from the current Windows machine state.
    /// This is used to build helper-managed environment variables for package launch.
    /// </summary>
    private RocmRuntimeContext ResolveRuntimeContext()
    {
        var state = ResolveWindowsMachineState();
        if (!state.IsCompatible)
        {
            return new RocmRuntimeContext
            {
                IsSupported = false,
                FailureReason = state.FailureReason,
                SelectedGpu = state.SelectedGpu,
                RuntimeGfxArch = state.RuntimeGfxArch,
            };
        }

        return new RocmRuntimeContext
        {
            IsSupported = true,
            SelectedGpu = state.SelectedGpu,
            RuntimeGfxArch = state.RuntimeGfxArch,
        };
    }

    /// <summary>
    /// Builds the final launch environment for a ROCm-capable package by combining helper defaults,
    /// package-specific environment values, and optional user overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildLaunchEnvironment(RocmPackageProfile profile)
    {
        var runtimeContext = ResolveRuntimeContext();

        if (!runtimeContext.IsSupported)
            return new Dictionary<string, string>();

        var helperEnvironment = BuildHelperLaunchEnvironment(runtimeContext, profile);
        var packageEnvironment =
            profile.ExtraEnvironmentFactory?.Invoke(runtimeContext) ?? new Dictionary<string, string>();

        var mergedEnvironment = MergeLaunchEnvironment(
            helperEnvironment,
            packageEnvironment,
            profile.EnvironmentOptions
        );

        return mergedEnvironment;
    }

    /// <summary>
    /// Returns the shared informational notice lines shown when launching Windows ROCm packages.
    /// </summary>
    public IReadOnlyList<string> GetWindowsLaunchNoticeLines()
    {
        return WindowsLaunchNoticeLines;
    }

    /// <summary>
    /// Ensures <c>rocm-sdk-devel</c> is installed from the ROCm multi-arch index.
    /// It prefers a build whose date token matches the installed ROCm torch build and falls back to the latest available build when no exact match is available.
    /// </summary>
    public async Task EnsureWindowsSdkDevelAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException(
                "torch is not installed in this environment. Install the Windows ROCm torch build first."
            );
        }

        if (!IsUsableWindowsNativeTorchBuild(torchInfo.Version, null))
        {
            throw new InvalidOperationException(
                $"Installed torch is not a usable Windows ROCm build (detected version: {torchInfo.Version})."
            );
        }

        var nightlyBuildDateToken = TryGetNightlyBuildDateToken(torchInfo.Version);
        var installedRocmSdkDevel = await venvRunner.PipShow(RocmSdkDevelPackageName).ConfigureAwait(false);
        if (
            !string.IsNullOrWhiteSpace(nightlyBuildDateToken)
            && HasNightlyBuildDateToken(installedRocmSdkDevel?.Version, nightlyBuildDateToken)
        )
        {
            return;
        }

        var indexResult = await venvRunner
            .PipIndex(RocmSdkDevelPackageName, WindowsRocmSupport.MultiArchPythonPackageIndexUrl)
            .ConfigureAwait(false);

        var latestVersion = indexResult?.AvailableVersions.FirstOrDefault();
        var matchingVersion = string.IsNullOrWhiteSpace(nightlyBuildDateToken)
            ? null
            : indexResult?.AvailableVersions.FirstOrDefault(version =>
                HasNightlyBuildDateToken(version, nightlyBuildDateToken)
            );
        var versionToInstall = matchingVersion ?? latestVersion;

        if (string.IsNullOrWhiteSpace(versionToInstall))
        {
            throw new InvalidOperationException(
                $"No {RocmSdkDevelPackageName} builds were found on the ROCm multi-arch index."
            );
        }

        if (!string.IsNullOrWhiteSpace(matchingVersion))
        {
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Installing {RocmSdkDevelPackageName} {matchingVersion} for Windows ROCm...",
                    isIndeterminate: true
                )
            );
        }
        else
        {
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Falling back to latest available {RocmSdkDevelPackageName} build {versionToInstall} for Windows ROCm...",
                    isIndeterminate: true
                )
            );
        }

        await venvRunner
            .PipInstall(
                new PipInstallArgs()
                    .AddArg("--upgrade")
                    .AddKeyedArgs(
                        "--index-url",
                        ["--index-url", WindowsRocmSupport.MultiArchPythonPackageIndexUrl]
                    )
                    .AddArg($"{RocmSdkDevelPackageName}=={versionToInstall}"),
                onConsoleOutput
            )
            .ConfigureAwait(false);

        _ = cancellationToken;
    }

    /// <summary>
    /// Builds the standard pip install config for helper-managed Windows ROCm package installs.
    /// </summary>
    public PipInstallConfig BuildWindowsNativeInstallConfig(RocmPackageProfile profile)
    {
        return profile.InstallConfig with { SkipTorchInstall = true };
    }

    /// <summary>
    /// Installs the ROCm torch wheel set from the multi-arch index and verifies that the resulting torch installation reports usable ROCm metadata.
    /// </summary>
    public async Task InstallWindowsNativeTorchAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var state = ResolveWindowsMachineState();
        if (!state.IsCompatible)
        {
            throw new InvalidOperationException(
                state.FailureReason ?? "Windows ROCm installation is not supported for the current machine."
            );
        }

        var multiArchDeviceExtra = state.MultiArchDeviceExtra;

        if (string.IsNullOrWhiteSpace(multiArchDeviceExtra))
        {
            throw new InvalidOperationException(
                $"No Windows ROCm multi-arch device extra is available for '{state.RuntimeGfxArch ?? "unknown"}'."
            );
        }

        progress?.Report(new ProgressReport(-1f, "Installing ROCm torch...", isIndeterminate: true));

        var installConfig = profile.InstallConfig;
        var torchArgs = new PipInstallArgs()
            .AddKeyedArgs("--index-url", ["--index-url", WindowsRocmSupport.MultiArchPythonPackageIndexUrl])
            .AddArgs(
                new Argument($"torch[{multiArchDeviceExtra}]"),
                new Argument($"torchvision[{multiArchDeviceExtra}]"),
                new Argument("torchaudio")
            );

        if (installConfig.UpgradePackages)
        {
            torchArgs = torchArgs.AddArg("--upgrade");
        }

        if (installConfig.ForceReinstallTorch)
        {
            torchArgs = torchArgs.AddArg("--force-reinstall");
        }

        if (installedPackage.PipOverrides != null)
        {
            torchArgs = torchArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        if (installConfig.PostTorchInstallPipArgs.Any())
        {
            var postTorchInstallPipArgs = new PipInstallArgs([.. installConfig.PostTorchInstallPipArgs]);

            if (installedPackage.PipOverrides != null)
            {
                postTorchInstallPipArgs = postTorchInstallPipArgs.WithUserOverrides(
                    installedPackage.PipOverrides
                );
            }

            await venvRunner.PipInstall(postTorchInstallPipArgs, onConsoleOutput).ConfigureAwait(false);
        }

        await VerifyWindowsNativeTorchInstallAsync(venvRunner, onConsoleOutput, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a compatibility result from the current machine state and package profile.
    /// This keeps the first ROCm helper slice focused on hardware capability and GPU selection only.
    /// </summary>
    private RocmCompatibilityResult BuildCompatibilityResult()
    {
        var state = ResolveWindowsMachineState();

        return new RocmCompatibilityResult
        {
            IsCompatible = state.IsCompatible,
            FailureReason = state.FailureReason,
            SelectedGpu = state.SelectedGpu,
            ResolvedGfxArch = state.RuntimeGfxArch,
        };
    }

    private RocmMachineState ResolveWindowsMachineState()
    {
        var amdGpus = GetAmdGpuCandidates(forceRefresh: true).ToList();
        if (amdGpus.Count == 0)
        {
            return new RocmMachineState
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU was detected for ROCm evaluation.",
            };
        }

        var supportedAmdGpus = amdGpus.Where(IsSupportedWindowsRocmGpu).ToList();
        if (supportedAmdGpus.Count == 0)
        {
            return new RocmMachineState
            {
                IsCompatible = false,
                FailureReason = GetUnsupportedGpuReason(amdGpus),
            };
        }

        var selectedGpu =
            TryResolvePreferredAmdGpu(supportedAmdGpus, settingsManager.Settings.PreferredGpu)
            ?? supportedAmdGpus.First();
        var runtimeGfxArch =
            WindowsRocmSupport.TryGetCanonicalArchitecture(selectedGpu.GetAmdGfxArch())
            ?? GetSupportedFallbackGfxArch(supportedAmdGpus);
        var isCompatible = !string.IsNullOrWhiteSpace(runtimeGfxArch);

        return new RocmMachineState
        {
            IsCompatible = isCompatible,
            FailureReason = isCompatible
                ? null
                : "No supported AMD GFX architecture could be resolved for ROCm.",
            SelectedGpu = selectedGpu,
            RuntimeGfxArch = runtimeGfxArch,
            MultiArchDeviceExtra = WindowsRocmSupport.TryGetMultiArchDeviceExtra(runtimeGfxArch),
        };
    }

    /// <summary>
    /// Returns AMD GPUs from Stability Matrix's internal hardware model.
    /// This is the canonical GPU source for the ROCm helper and intentionally avoids package-local probing.
    /// </summary>
    private static IReadOnlyList<GpuInfo> GetAmdGpuCandidates(bool forceRefresh = false)
    {
        return HardwareHelper.IterGpuInfo(forceRefresh).Where(gpu => gpu.IsAmd).ToList();
    }

    /// <summary>
    /// Resolves the preferred AMD GPU when the configured preference is still present in the current hardware list.
    /// </summary>
    private static GpuInfo? TryResolvePreferredAmdGpu(
        IEnumerable<GpuInfo> availableGpus,
        GpuInfo? preferredGpu
    )
    {
        if (preferredGpu is null || !preferredGpu.IsAmd)
            return null;

        var preferredMatch = availableGpus.FirstOrDefault(gpu => gpu.Equals(preferredGpu));
        if (preferredMatch is not null)
            return preferredMatch;

        if (!string.IsNullOrWhiteSpace(preferredGpu.Name))
        {
            Logger.Info(
                "Preferred GPU {PreferredGpuName} was ignored for ROCm detection because it is not present in current hardware enumeration.",
                preferredGpu.Name
            );
        }

        return null;
    }

    /// <summary>
    /// Resolves the preferred AMD GFX architecture when the configured GPU is supported and currently present.
    /// </summary>
    private static string? TryResolvePreferredAmdGfxArch(
        IEnumerable<GpuInfo> availableGpus,
        GpuInfo? preferredGpu
    )
    {
        var resolvedPreferredGpu = TryResolvePreferredAmdGpu(availableGpus, preferredGpu);
        return resolvedPreferredGpu is not null && IsSupportedWindowsRocmGpu(resolvedPreferredGpu)
            ? WindowsRocmSupport.TryGetCanonicalArchitecture(resolvedPreferredGpu.GetAmdGfxArch())
            : null;
    }

    /// <summary>
    /// Resolves the first supported AMD GFX architecture from the current machine state when no preferred GPU applies.
    /// </summary>
    private static string? GetSupportedFallbackGfxArch(IEnumerable<GpuInfo> availableGpus)
    {
        return availableGpus
            .Where(IsSupportedWindowsRocmGpu)
            .Select(gpu => WindowsRocmSupport.TryGetCanonicalArchitecture(gpu.GetAmdGfxArch()))
            .FirstOrDefault(IsSupportedWindowsRocmArchitecture);
    }

    /// <summary>
    /// Determines whether a GPU is supported by the Windows ROCm install flow currently modeled by the helper.
    /// </summary>
    private static bool IsSupportedWindowsRocmGpu(GpuInfo gpu)
    {
        return WindowsRocmSupport.IsSupportedGpu(gpu);
    }

    /// <summary>
    /// Determines whether a resolved AMD GFX architecture falls inside the Windows ROCm support set currently modeled by the helper.
    /// </summary>
    private static bool IsSupportedWindowsRocmArchitecture(string? gfxArch)
    {
        return WindowsRocmSupport.IsSupportedArchitecture(gfxArch);
    }

    /// <summary>
    /// Produces a readable incompatibility reason when AMD hardware is present but not usable for Windows ROCm.
    /// </summary>
    private static string GetUnsupportedGpuReason(IReadOnlyList<GpuInfo> amdGpus)
    {
        _ = amdGpus;
        return "No AMD GPU with a supported Windows ROCm architecture was detected.";
    }

    /// <summary>
    /// Verifies that the installed torch build still reports usable ROCm metadata after helper-managed installs complete.
    /// </summary>
    private static async Task VerifyWindowsNativeTorchInstallAsync(
        IPyVenvRunner venvRunner,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException("torch was not installed after Windows ROCm setup.");
        }

        var verificationResult = await venvRunner
            .Run(
                "-c \"import json, torch; print(json.dumps({'version': torch.__version__, 'hip': torch.version.hip, 'cuda': torch.cuda.is_available()}))\""
            )
            .ConfigureAwait(false);

        var verificationOutput = (verificationResult.StandardOutput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(verificationOutput))
        {
            throw new InvalidOperationException("Torch verification produced no output.");
        }

        var verificationJson = TryExtractJsonObject(verificationOutput);
        if (string.IsNullOrWhiteSpace(verificationJson))
        {
            throw new InvalidOperationException(
                $"Unexpected torch verification output: {verificationOutput}"
            );
        }

        JsonDocument verificationDocument;
        try
        {
            verificationDocument = JsonDocument.Parse(verificationJson);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Unexpected torch verification output: {verificationOutput}",
                exception
            );
        }

        using (verificationDocument)
        {
            var root = verificationDocument.RootElement;
            var version = root.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
            var hipVersion = root.TryGetProperty("hip", out var hipElement) ? hipElement.GetString() : null;
            var cudaAvailable = root.TryGetProperty("cuda", out var cudaElement) && cudaElement.GetBoolean();

            if (!IsUsableWindowsNativeTorchBuild(version, hipVersion))
            {
                throw new InvalidOperationException(
                    $"Installed torch is not a usable ROCm build. Verification output: {verificationOutput}"
                );
            }

            if (!cudaAvailable)
            {
                onConsoleOutput?.Invoke(
                    ProcessOutput.FromStdErrLine(
                        $"Torch verification warning: installed ROCm torch build reported cuda={cudaAvailable}; continuing because ROCm metadata was detected (version={version}, hip={hipVersion})."
                    )
                );
            }

            onConsoleOutput?.Invoke(
                ProcessOutput.FromStdOutLine(
                    $"Torch verification: version={version}, hip={hipVersion}, cuda={cudaAvailable}"
                )
            );
        }

        _ = cancellationToken;
    }

    internal static bool IsUsableWindowsNativeTorchBuild(string? version, string? hipVersion)
    {
        if (!string.IsNullOrWhiteSpace(hipVersion))
            return true;

        return !string.IsNullOrWhiteSpace(version)
            && version.Contains("rocm", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetNightlyBuildDateToken(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var devIndex = version.IndexOf("dev", StringComparison.OrdinalIgnoreCase);
        if (devIndex < 0)
            return null;

        var startIndex = devIndex + 3;
        if (version.Length < startIndex + 8)
            return null;

        var token = version.Substring(startIndex, 8);
        return token.All(char.IsDigit) ? token : null;
    }

    private static bool HasNightlyBuildDateToken(string? version, string nightlyBuildDateToken)
    {
        return !string.IsNullOrWhiteSpace(version)
            && !string.IsNullOrWhiteSpace(nightlyBuildDateToken)
            && version.Contains($"dev{nightlyBuildDateToken}", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TryExtractJsonObject(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmedOutput = output.Trim();

        for (var index = 0; index < trimmedOutput.Length; index++)
        {
            if (trimmedOutput[index] != '{')
                continue;

            try
            {
                using var document = JsonDocument.Parse(trimmedOutput[index..]);
                return document.RootElement.GetRawText();
            }
            catch (JsonException) { }
        }

        return null;
    }

    /// <summary>
    /// Builds helper-owned ROCm launch variables from the resolved runtime context and package profile.
    /// </summary>
    private IReadOnlyDictionary<string, string> BuildHelperLaunchEnvironment(
        RocmRuntimeContext runtimeContext,
        RocmPackageProfile profile
    )
    {
        var environment = new Dictionary<string, string>(EnvComparer);
        var options = profile.EnvironmentOptions;
        var gfxArch = runtimeContext.RuntimeGfxArch;

        ApplyDefaultLaunchEnvironment(environment, gfxArch, options);

        return environment;
    }

    private void ApplyDefaultLaunchEnvironment(
        IDictionary<string, string> environment,
        string? gfxArch,
        RocmEnvironmentOptions options
    )
    {
        SetIfNotNull(environment, "FLASH_ATTENTION_TRITON_AMD_ENABLE", options.FlashAttentionTritonAmdEnable);
        SetIfNotNull(environment, "MIOPEN_FIND_MODE", options.MiopenFindMode);
        SetIfNotNull(environment, "MIOPEN_SEARCH_CUTOFF", options.MiopenSearchCutoff);
        SetIfNotNull(environment, "MIOPEN_FIND_ENFORCE", options.MiopenFindEnforce);
        SetIfNotNull(environment, "PYTORCH_ALLOC_CONF", options.PyTorchAllocConf);

        if (options.ApplyAotritonExperimental && WindowsRocmSupport.SupportsAotritonExperimental(gfxArch))
        {
            environment["TORCH_ROCM_AOTRITON_ENABLE_EXPERIMENTAL"] = "1";
        }

        if (options.ApplyLegacySdpFallback && WindowsRocmSupport.IsLegacyArchitecture(gfxArch))
        {
            environment["TORCH_BACKENDS_CUDA_FLASH_SDP_ENABLED"] = "0";
            environment["TORCH_BACKENDS_CUDA_MEM_EFF_SDP_ENABLED"] = "0";
            environment["TORCH_BACKENDS_CUDA_MATH_SDP_ENABLED"] = "1";
        }
    }

    private static void SetIfNotNull(IDictionary<string, string> environment, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment[key] = value;
        }
    }

    /// <summary>
    /// Merges helper-owned and package-specific launch environment variables.
    /// </summary>
    private IReadOnlyDictionary<string, string> MergeLaunchEnvironment(
        IReadOnlyDictionary<string, string> helperEnvironment,
        IReadOnlyDictionary<string, string> packageEnvironment,
        RocmEnvironmentOptions options
    )
    {
        var merged = new Dictionary<string, string>(EnvComparer);

        foreach (var source in new[] { helperEnvironment, packageEnvironment })
        {
            if (ReferenceEquals(source, packageEnvironment) && !options.IncludePackageOverrides)
                continue;

            foreach (var pair in source)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        if (
            options.IncludeUserOverrides
            && settingsManager.Settings.EnvironmentVariables is { Count: > 0 } userOverrides
        )
        {
            foreach (var pair in userOverrides)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}
