using System.Collections.Immutable;
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

    /// <inheritdoc />
    public RocmCompatibilityResult GetCompatibility(RocmPackageProfile profile)
    {
        return BuildCompatibilityResult(profile);
    }

    /// <inheritdoc />
    public RocmRuntimeContext ResolveRuntimeContext(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    )
    {
        _ = installLocation;
        _ = installedPackage;

        var compatibility = BuildCompatibilityResult(profile);
        if (!compatibility.IsCompatible)
        {
            return new RocmRuntimeContext
            {
                IsSupported = false,
                FailureReason = compatibility.FailureReason,
                SelectedGpu = compatibility.SelectedGpu,
                RuntimeGfxArch = compatibility.ResolvedGfxArch,
            };
        }

        var supportedAmdGpus = GetAmdGpuCandidates(forceRefresh: true)
            .Where(IsSupportedWindowsRocmGpu)
            .ToList();

        var selectedGpu =
            compatibility.SelectedGpu
            ?? TryResolvePreferredAmdGpu(supportedAmdGpus, settingsManager.Settings.PreferredGpu)
            ?? supportedAmdGpus.FirstOrDefault();

        var runtimeGfxArch =
            compatibility.ResolvedGfxArch
            ?? selectedGpu?.GetAmdGfxArch()
            ?? GetSupportedFallbackGfxArch(supportedAmdGpus);

        return new RocmRuntimeContext
        {
            IsSupported = true,
            SelectedGpu = selectedGpu,
            RuntimeGfxArch = runtimeGfxArch,
        };
    }

    /// <inheritdoc />
    public RocmInstallContext ResolveInstallContext(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    )
    {
        _ = installLocation;
        _ = installedPackage;

        var supportedAmdGpus = GetAmdGpuCandidates(forceRefresh: true)
            .Where(IsSupportedWindowsRocmGpu)
            .ToList();

        var preferredGfxArch = TryResolvePreferredAmdGfxArch(
            supportedAmdGpus,
            settingsManager.Settings.PreferredGpu
        );

        var runtimeGfxArch = preferredGfxArch ?? GetSupportedFallbackGfxArch(supportedAmdGpus);
        var windowsNativeIndexUrl = WindowsRocmSupport.TryGetPackageIndexUrl(runtimeGfxArch);

        return new RocmInstallContext
        {
            RuntimeGfxArch = runtimeGfxArch,
            RocmPackageIndexUrl = windowsNativeIndexUrl,
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> BuildLaunchEnvironment(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    )
    {
        var runtimeContext = ResolveRuntimeContext(installLocation, installedPackage, profile);

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
            throw new ApplicationException(
                compatibility.FailureReason
                    ?? "Windows ROCm installation is not supported for the current machine."
            );
        }

        var installContext = ResolveInstallContext(installLocation, installedPackage, profile);

        var rocmPackageIndexUrl = installContext.RocmPackageIndexUrl;

        if (string.IsNullOrWhiteSpace(rocmPackageIndexUrl))
        {
            throw new ApplicationException(
                $"No Windows ROCm Technical Preview index URL is available for '{installContext.RuntimeGfxArch ?? "unknown"}'."
            );
        }

        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        if (profile.RequiresRocmSdk)
        {
            progress?.Report(new ProgressReport(-1f, "Installing ROCm runtime...", isIndeterminate: true));
            var rocmRuntimeArgs = new PipInstallArgs()
                .AddKeyedArgs("--index-url", ["--index-url", rocmPackageIndexUrl])
                .AddArgs("rocm[devel,libraries]");

            if (installedPackage.PipOverrides != null)
            {
                rocmRuntimeArgs = rocmRuntimeArgs.WithUserOverrides(installedPackage.PipOverrides);
            }

            await venvRunner.PipInstall(rocmRuntimeArgs, onConsoleOutput).ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Initializing ROCm SDK...", isIndeterminate: true));
            await InitializeWindowsNativeRocmSdkAsync(installLocation, onConsoleOutput, cancellationToken)
                .ConfigureAwait(false);
        }

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
            .AddArg("--pre")
            .AddArg("--upgrade")
            .AddKeyedArgs("--index-url", ["--index-url", rocmPackageIndexUrl])
            .WithTorch()
            .WithTorchAudio()
            .WithTorchVision();

        if (profile.ForceReinstallTorch)
        {
            torchArgs = torchArgs.AddArg("--force-reinstall");
        }

        if (installedPackage.PipOverrides != null)
        {
            torchArgs = torchArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        if (profile.RequiresRocmSdk)
        {
            await AlignRocmSdkDevelVersionAsync(venvRunner, rocmPackageIndexUrl, onConsoleOutput)
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Reinitializing ROCm SDK...", isIndeterminate: true));
            await InitializeWindowsNativeRocmSdkAsync(installLocation, onConsoleOutput, cancellationToken)
                .ConfigureAwait(false);
        }

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

        if (profile.RequiresRocmSdk)
        {
            await VerifyWindowsNativeRocmRuntimeAsync(installLocation, onConsoleOutput, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds a compatibility result from the current machine state and package profile.
    /// This keeps the first ROCm helper slice focused on hardware capability and GPU selection only.
    /// </summary>
    private RocmCompatibilityResult BuildCompatibilityResult(RocmPackageProfile profile)
    {
        var amdGpus = GetAmdGpuCandidates(forceRefresh: true).ToList();
        if (amdGpus.Count == 0)
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU was detected for ROCm evaluation.",
            };
        }

        var preferredGpu = settingsManager.Settings.PreferredGpu;

        var supportedAmdGpus = amdGpus.Where(IsSupportedWindowsRocmGpu).ToList();
        if (supportedAmdGpus.Count == 0)
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = GetUnsupportedGpuReason(amdGpus),
            };
        }

        var selectedGpu =
            TryResolvePreferredAmdGpu(supportedAmdGpus, preferredGpu) ?? supportedAmdGpus.First();
        var resolvedGfxArch = selectedGpu.GetAmdGfxArch() ?? GetSupportedFallbackGfxArch(supportedAmdGpus);

        return new RocmCompatibilityResult
        {
            IsCompatible = !string.IsNullOrWhiteSpace(resolvedGfxArch),
            FailureReason = string.IsNullOrWhiteSpace(resolvedGfxArch)
                ? "No supported AMD GFX architecture could be resolved for ROCm."
                : null,
            SelectedGpu = selectedGpu,
            ResolvedGfxArch = resolvedGfxArch,
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
            ? resolvedPreferredGpu.GetAmdGfxArch()
            : null;
    }

    /// <summary>
    /// Resolves the first supported AMD GFX architecture from the current machine state when no preferred GPU applies.
    /// </summary>
    private static string? GetSupportedFallbackGfxArch(IEnumerable<GpuInfo> availableGpus)
    {
        return availableGpus
            .Where(IsSupportedWindowsRocmGpu)
            .Select(gpu => gpu.GetAmdGfxArch())
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

    /// <summary>
    /// Runs <c>rocm-sdk init</c> after the helper-managed runtime packages are installed so the Windows ROCm SDK can prepare the venv.
    /// </summary>
    private static async Task InitializeWindowsNativeRocmSdkAsync(
        string installLocation,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        var rocmSdkExe = Path.Combine(installLocation, "venv", "Scripts", "rocm-sdk.exe");
        if (!File.Exists(rocmSdkExe))
        {
            throw new FileNotFoundException("rocm-sdk.exe was not installed", rocmSdkExe);
        }

        using var rocmSdkProcess = ProcessRunner.StartAnsiProcess(
            rocmSdkExe,
            ["init"],
            installLocation,
            onConsoleOutput
        );

        await rocmSdkProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (rocmSdkProcess.ExitCode != 0)
        {
            throw new ProcessException($"rocm-sdk init failed with code {rocmSdkProcess.ExitCode}");
        }
    }

    /// <summary>
    /// Uses AMD's bundled <c>hipInfo.exe</c> to confirm the installed Windows ROCm runtime can enumerate a ROCm-capable GPU.
    /// </summary>
    private static async Task VerifyWindowsNativeRocmRuntimeAsync(
        string installLocation,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        var rocmSdkExe = Path.Combine(installLocation, "venv", "Scripts", "rocm-sdk.exe");
        if (!File.Exists(rocmSdkExe))
        {
            throw new FileNotFoundException("rocm-sdk.exe was not installed", rocmSdkExe);
        }

        var rocmBinResult = await ProcessRunner
            .GetProcessResultAsync(rocmSdkExe, ["path", "--bin"], installLocation, useUtf8Encoding: true)
            .ConfigureAwait(false);

        var rocmBinPath = (rocmBinResult.StandardOutput ?? string.Empty).Trim();
        if (!rocmBinResult.IsSuccessExitCode || string.IsNullOrWhiteSpace(rocmBinPath))
        {
            var rocmBinOutput = CombineProcessOutput(
                rocmBinResult.StandardOutput,
                rocmBinResult.StandardError
            );
            throw new ApplicationException(
                $"ROCm runtime verification failed while resolving the ROCm SDK bin path. Output: {rocmBinOutput}"
            );
        }

        var hipInfoExe = Path.Combine(rocmBinPath, $"hipInfo{Compat.ExeExtension}");
        if (!File.Exists(hipInfoExe))
        {
            throw new FileNotFoundException(
                "hipInfo.exe was not found in the ROCm SDK bin directory",
                hipInfoExe
            );
        }

        var hipInfoResult = await ProcessRunner
            .GetProcessResultAsync(
                hipInfoExe,
                [],
                installLocation,
                new Dictionary<string, string> { ["PATH"] = rocmBinPath },
                useUtf8Encoding: true
            )
            .ConfigureAwait(false);

        var hipInfoOutput = CombineProcessOutput(hipInfoResult.StandardOutput, hipInfoResult.StandardError);
        if (!hipInfoResult.IsSuccessExitCode)
        {
            var runtimeFailureReason = TryGetWindowsNativeRocmRuntimeFailureReason(hipInfoOutput);
            throw new ApplicationException(
                runtimeFailureReason is null
                    ? $"ROCm runtime verification failed while probing the installed runtime with hipInfo.exe. Output: {hipInfoOutput}"
                    : $"ROCm runtime verification failed: {runtimeFailureReason} Output: {hipInfoOutput}"
            );
        }

        onConsoleOutput?.Invoke(
            ProcessOutput.FromStdOutLine(
                $"ROCm runtime verification succeeded via hipInfo.exe: {hipInfoOutput}"
            )
        );

        _ = cancellationToken;
    }

    /// <summary>
    /// Reinstalls <c>rocm-sdk-devel</c> to the resolved ROCm build version when the torch step downgrades the runtime stack.
    /// </summary>
    private static async Task AlignRocmSdkDevelVersionAsync(
        IPyVenvRunner venvRunner,
        string rocmPackageIndexUrl,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        var rocmInfo = await venvRunner.PipShow("rocm").ConfigureAwait(false);
        var rocmSdkDevelInfo = await venvRunner.PipShow("rocm-sdk-devel").ConfigureAwait(false);
        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);

        var targetVersion = GetRocmSdkDevelAlignmentVersion(
            rocmInfo?.Version,
            rocmSdkDevelInfo?.Version,
            torchInfo?.Version
        );

        if (string.IsNullOrWhiteSpace(targetVersion))
            return;

        onConsoleOutput?.Invoke(
            ProcessOutput.FromStdErrLine(
                $"Aligning rocm-sdk-devel from version={rocmSdkDevelInfo?.Version ?? "not-installed"} to version={targetVersion} to match the resolved ROCm torch/runtime build."
            )
        );

        var alignmentArgs = new PipInstallArgs()
            .AddKeyedArgs("--index-url", ["--index-url", rocmPackageIndexUrl])
            .AddArg("--force-reinstall")
            .AddArg($"rocm-sdk-devel=={targetVersion}");

        await venvRunner.PipInstall(alignmentArgs, onConsoleOutput).ConfigureAwait(false);
    }

    internal static bool IsUsableWindowsNativeTorchBuild(string? version, string? hipVersion)
    {
        if (!string.IsNullOrWhiteSpace(hipVersion))
            return true;

        return !string.IsNullOrWhiteSpace(version)
            && version.Contains("rocm", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? GetRocmSdkDevelAlignmentVersion(
        string? rocmVersion,
        string? rocmSdkDevelVersion,
        string? torchVersion = null
    )
    {
        var targetVersion = !string.IsNullOrWhiteSpace(rocmVersion)
            ? rocmVersion
            : TryExtractRocmBuildVersion(torchVersion);

        if (string.IsNullOrWhiteSpace(targetVersion))
            return null;

        return string.Equals(targetVersion, rocmSdkDevelVersion, StringComparison.OrdinalIgnoreCase)
            ? null
            : targetVersion;
    }

    internal static string? TryGetWindowsNativeRocmRuntimeFailureReason(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        if (output.Contains("no ROCm-capable device is detected", StringComparison.OrdinalIgnoreCase))
        {
            return "the installed ROCm runtime could not detect a ROCm-capable GPU on this system.";
        }

        if (output.Contains("No WDDM adapters found", StringComparison.OrdinalIgnoreCase))
        {
            return "the ROCm runtime could not find any compatible WDDM adapters for the current GPU/driver stack.";
        }

        return null;
    }

    internal static string? TryExtractRocmBuildVersion(string? torchVersion)
    {
        if (string.IsNullOrWhiteSpace(torchVersion))
            return null;

        var rocmMarkerIndex = torchVersion.IndexOf("rocm", StringComparison.OrdinalIgnoreCase);
        if (rocmMarkerIndex < 0)
            return null;

        var rocmBuildVersion = torchVersion[(rocmMarkerIndex + "rocm".Length)..].Trim();
        return string.IsNullOrWhiteSpace(rocmBuildVersion) ? null : rocmBuildVersion;
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

    internal static string CombineProcessOutput(string? standardOutput, string? standardError)
    {
        var sections = new[] { standardOutput?.Trim(), standardError?.Trim() }.Where(section =>
            !string.IsNullOrWhiteSpace(section)
        );

        return string.Join(Environment.NewLine, sections);
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

        if (options.ApplyAotritonExperimental && WindowsRocmSupport.IsModernArchitecture(gfxArch))
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
}
