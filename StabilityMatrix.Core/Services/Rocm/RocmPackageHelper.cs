using System.Text.Json;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
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
    private static readonly string[] WindowsLaunchNoticeLines =
    [
        "Stability Matrix Windows ROCm Notice: Windows AMD ROCm support is experimental. Please report any issues to Stability Matrix first so it can be determined whether the issue is package-specific.",
        "Because this setup may not be officially supported by package developers, only contact upstream support for issues clearly caused by the package itself.",
    ];

    /// <inheritdoc />
    public RocmCompatibilityResult GetCompatibility(RocmPackageProfile profile)
    {
        _ = profile;
        return BuildCompatibilityResult(profile);
    }

    /// <inheritdoc />
    private RocmRuntimeContext ResolveRuntimeContext(RocmPackageProfile profile)
    {
        _ = profile;

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

    /// <inheritdoc />
    private RocmInstallContext ResolveInstallContext(RocmPackageProfile profile)
    {
        _ = profile;

        var state = ResolveWindowsMachineState();

        return new RocmInstallContext
        {
            RuntimeGfxArch = state.RuntimeGfxArch,
            MultiArchDeviceExtra = state.MultiArchDeviceExtra,
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> BuildLaunchEnvironment(RocmPackageProfile profile)
    {
        var runtimeContext = ResolveRuntimeContext(profile);

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

    /// <inheritdoc />
    public IReadOnlyList<string> GetWindowsLaunchNoticeLines()
    {
        return WindowsLaunchNoticeLines;
    }

    /// <inheritdoc />
    public async Task InstallWindowsNativePackageAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var compatibility = GetCompatibility(profile);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(
                compatibility.FailureReason
                    ?? "Windows ROCm installation is not supported for the current machine."
            );
        }

        var installContext = ResolveInstallContext(profile);

        var multiArchDeviceExtra = installContext.MultiArchDeviceExtra;

        if (string.IsNullOrWhiteSpace(multiArchDeviceExtra))
        {
            throw new ApplicationException(
                $"No Windows ROCm multi-arch device extra is available for '{installContext.RuntimeGfxArch ?? "unknown"}'."
            );
        }

        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, "Installing package requirements...", isIndeterminate: true)
        );

        var requirementsPipArgs = new PipInstallArgs([.. profile.ExtraInstallPipArgs]);
        if (profile.UpgradePackages)
        {
            requirementsPipArgs = requirementsPipArgs.AddArg("--upgrade");
        }

        foreach (var relativePath in profile.RequirementsFilePaths)
        {
            var requirementsFile = new FilePath(venvRunner.WorkingDirectory ?? installLocation, relativePath);
            if (!requirementsFile.Exists)
                continue;

            var requirementsContent = await requirementsFile
                .ReadAllTextAsync(cancellationToken)
                .ConfigureAwait(false);

            requirementsPipArgs = requirementsPipArgs.WithParsedFromRequirementsTxt(
                requirementsContent,
                profile.RequirementsExcludePattern
            );
        }

        if (installedPackage.PipOverrides != null)
        {
            requirementsPipArgs = requirementsPipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(requirementsPipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing ROCm torch...", isIndeterminate: true));

        var torchArgs = new PipInstallArgs()
            .AddArg("--upgrade")
            .AddKeyedArgs("--index-url", ["--index-url", WindowsRocmSupport.MultiArchPythonPackageIndexUrl])
            .AddArgs(
                new Argument($"torch[{multiArchDeviceExtra}]"),
                new Argument($"torchvision[{multiArchDeviceExtra}]"),
                new Argument("torchaudio")
            );

        if (profile.ForceReinstallTorch)
        {
            torchArgs = torchArgs.AddArg("--force-reinstall");
        }

        if (installedPackage.PipOverrides != null)
        {
            torchArgs = torchArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);
        if (profile.PostInstallPipArgs.Any())
        {
            var postInstallPipArgs = new PipInstallArgs([.. profile.PostInstallPipArgs]);
            if (installedPackage.PipOverrides != null)
            {
                postInstallPipArgs = postInstallPipArgs.WithUserOverrides(installedPackage.PipOverrides);
            }

            await venvRunner.PipInstall(postInstallPipArgs, onConsoleOutput).ConfigureAwait(false);
        }

        await VerifyWindowsNativeTorchInstallAsync(venvRunner, onConsoleOutput, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a compatibility result from the current machine state and package profile.
    /// This keeps the first ROCm helper slice focused on hardware capability and GPU selection only.
    /// </summary>
    private RocmCompatibilityResult BuildCompatibilityResult(RocmPackageProfile profile)
    {
        _ = profile;
        var state = ResolveWindowsMachineState();

        return new RocmCompatibilityResult
        {
            IsCompatible = state.IsCompatible,
            FailureReason = state.FailureReason,
            SelectedGpu = state.SelectedGpu,
            ResolvedGfxArch = state.RuntimeGfxArch,
        };
    }

    private ResolvedWindowsRocmState ResolveWindowsMachineState()
    {
        var amdGpus = GetAmdGpuCandidates(forceRefresh: true).ToList();
        if (amdGpus.Count == 0)
        {
            return new ResolvedWindowsRocmState
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU was detected for ROCm evaluation.",
            };
        }

        var supportedAmdGpus = amdGpus.Where(IsSupportedWindowsRocmGpu).ToList();
        if (supportedAmdGpus.Count == 0)
        {
            return new ResolvedWindowsRocmState
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

        return new ResolvedWindowsRocmState
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
            throw new ApplicationException("torch was not installed after Windows ROCm setup.");
        }

        var verificationResult = await venvRunner
            .Run(
                "-c \"import json, torch; print(json.dumps({'version': torch.__version__, 'hip': torch.version.hip, 'cuda': torch.cuda.is_available()}))\""
            )
            .ConfigureAwait(false);

        var verificationOutput = (verificationResult.StandardOutput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(verificationOutput))
        {
            throw new ApplicationException("Torch verification produced no output.");
        }

        var verificationJson = TryExtractJsonObject(verificationOutput);
        if (string.IsNullOrWhiteSpace(verificationJson))
        {
            throw new ApplicationException($"Unexpected torch verification output: {verificationOutput}");
        }

        JsonDocument verificationDocument;
        try
        {
            verificationDocument = JsonDocument.Parse(verificationJson);
        }
        catch (Exception exception)
        {
            throw new ApplicationException(
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
                throw new ApplicationException(
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

        if (options.ApplyRdna1Override && WindowsRocmSupport.IsRdna1Architecture(gfxArch))
        {
            environment["HSA_OVERRIDE_GFX_VERSION"] = "10.1.0";
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

    private sealed class ResolvedWindowsRocmState
    {
        public bool IsCompatible { get; init; }

        public string? FailureReason { get; init; }

        public GpuInfo? SelectedGpu { get; init; }

        public string? RuntimeGfxArch { get; init; }

        public string? MultiArchDeviceExtra { get; init; }
    }
}
