using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.PackageModification;

public enum WindowsRocmPackageCommandType
{
    SageAttention,
    DevelopmentSdk,
    BitsAndBytes,
    FlashAttention,
}

public class InstallWindowsRocmPackageCommandStep(
    IDownloadService downloadService,
    IPyInstallationManager pyInstallationManager,
    IPrerequisiteHelper prerequisiteHelper,
    IRocmPackageHelper rocmPackageHelper
) : IPackageStep
{
    private const string BitsAndBytesWheelUrl =
        "https://github.com/0xDELUXA/bitsandbytes_win_rocm/releases/download/0.50.0.dev0-py3-rocm7-win_amd64_all/bitsandbytes-0.50.0.dev0-cp312-cp312-win_amd64.whl";
    private const string AmdAiterWheelUrl =
        "https://github.com/0xDELUXA/flash-attention/releases/download/v2.8.4_win-rocm/amd_aiter-0.0.0-py3-none-win_amd64.whl";
    private const string FlashAttentionWheelUrl =
        "https://github.com/0xDELUXA/flash-attention/releases/download/v2.8.4_win-rocm/flash_attn-2.8.4-py3-none-win_amd64.whl";
    private const string TritonWindowsVersion = "3.6.0.post25";
    private const string SageAttentionVersion = "1.0.6";

    private const string AttnQkInt8PerBlockUrl =
        "https://raw.githubusercontent.com/patientx/ComfyUI-Zluda/refs/heads/master/comfy/customzluda/sa/attn_qk_int8_per_block.py";

    private const string AttnQkInt8PerBlockCausalUrl =
        "https://raw.githubusercontent.com/patientx/ComfyUI-Zluda/refs/heads/master/comfy/customzluda/sa/attn_qk_int8_per_block_causal.py";

    private const string QuantPerBlockUrl =
        "https://raw.githubusercontent.com/patientx/ComfyUI-Zluda/refs/heads/master/comfy/customzluda/sa/quant_per_block.py";

    public required InstalledPackage InstalledPackage { get; init; }
    public required DirectoryPath WorkingDirectory { get; init; }
    public required WindowsRocmPackageCommandType CommandType { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public string ProgressTitle =>
        CommandType switch
        {
            WindowsRocmPackageCommandType.SageAttention => "Installing Windows ROCm SageAttention",
            WindowsRocmPackageCommandType.DevelopmentSdk => "Installing Windows ROCm Development SDK",
            WindowsRocmPackageCommandType.BitsAndBytes => "Installing Windows ROCm bitsandbytes",
            WindowsRocmPackageCommandType.FlashAttention => "Installing Windows ROCm Flash Attention",
            _ => "Running Windows ROCm package command",
        };

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows ROCm package commands are only supported on Windows."
            );
        }

        if (InstalledPackage.FullPath is null)
        {
            throw new InvalidOperationException("Installed package path is not available.");
        }

        var venvDir = WorkingDirectory.JoinDir("venv");
        if (!venvDir.Exists)
        {
            throw new DirectoryNotFoundException($"ComfyUI venv was not found at '{venvDir.FullPath}'.");
        }

        var pyVersion = PyVersion.Parse(InstalledPackage.PythonVersion);
        if (pyVersion.StringValue == "0.0.0")
        {
            pyVersion = PyInstallationManager.Python_3_10_11;
        }

        var baseInstall = !string.IsNullOrWhiteSpace(InstalledPackage.PythonVersion)
            ? new PyBaseInstall(
                await pyInstallationManager.GetInstallationAsync(pyVersion).ConfigureAwait(false)
            )
            : PyBaseInstall.Default;

        await using var venvRunner = baseInstall.CreateVenvRunner(
            venvDir,
            workingDirectory: WorkingDirectory,
            environmentVariables: EnvironmentVariables
        );

        switch (CommandType)
        {
            case WindowsRocmPackageCommandType.SageAttention:
                await ExecuteSageAttentionAsync(venvRunner, progress).ConfigureAwait(false);
                break;
            case WindowsRocmPackageCommandType.DevelopmentSdk:
                await ExecuteDevelopmentSdkAsync(venvRunner, progress).ConfigureAwait(false);
                break;
            case WindowsRocmPackageCommandType.BitsAndBytes:
                await ExecuteBitsAndBytesAsync(venvRunner, pyVersion, progress).ConfigureAwait(false);
                break;
            case WindowsRocmPackageCommandType.FlashAttention:
                await ExecuteFlashAttentionAsync(venvRunner, progress).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Windows ROCm package command type: {CommandType}."
                );
        }
    }

    private void EnsureRocmCompatibility()
    {
        var compatibility = rocmPackageHelper.GetCompatibility(ComfyWindowsRocmProfile.Profile);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(
                compatibility.FailureReason
                    ?? "Windows ROCm package commands require a supported Windows ROCm machine state."
            );
        }
    }

    private async Task EnsureVcBuildToolsAsync(IProgress<ProgressReport>? progress)
    {
        if (!prerequisiteHelper.IsVcBuildToolsInstalled)
        {
            await prerequisiteHelper
                .InstallPackageRequirements([PackagePrerequisite.VcBuildTools], progress: progress)
                .ConfigureAwait(false);
        }
    }

    private async Task ExecuteDevelopmentSdkAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress
    )
    {
        EnsureRocmCompatibility();
        await rocmPackageHelper.EnsureWindowsSdkDevelAsync(venvRunner, progress).ConfigureAwait(false);
    }

    private async Task ExecuteSageAttentionAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress
    )
    {
        EnsureRocmCompatibility();
        await EnsureVcBuildToolsAsync(progress).ConfigureAwait(false);
        await rocmPackageHelper.EnsureWindowsSdkDevelAsync(venvRunner, progress).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(
                -1f,
                "Installing triton-windows for Windows ROCm SageAttention...",
                isIndeterminate: true
            )
        );
        await venvRunner.PipInstall($"triton-windows=={TritonWindowsVersion}").ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, "Installing SageAttention for Windows ROCm...", isIndeterminate: true)
        );
        await venvRunner.PipInstall($"--no-deps sageattention=={SageAttentionVersion}").ConfigureAwait(false);

        var sageAttentionDir = WorkingDirectory.JoinDir("venv", "Lib", "site-packages", "sageattention");
        if (!sageAttentionDir.Exists)
        {
            throw new DirectoryNotFoundException(
                $"Installed SageAttention package path was not found at '{sageAttentionDir.FullPath}'."
            );
        }

        progress?.Report(
            new ProgressReport(-1f, "Patching SageAttention for Windows ROCm...", isIndeterminate: true)
        );

        await DownloadAndReplaceFileAsync(
                sageAttentionDir,
                "attn_qk_int8_per_block.py",
                AttnQkInt8PerBlockUrl,
                progress
            )
            .ConfigureAwait(false);
        await DownloadAndReplaceFileAsync(
                sageAttentionDir,
                "attn_qk_int8_per_block_causal.py",
                AttnQkInt8PerBlockCausalUrl,
                progress
            )
            .ConfigureAwait(false);
        await DownloadAndReplaceFileAsync(sageAttentionDir, "quant_per_block.py", QuantPerBlockUrl, progress)
            .ConfigureAwait(false);
    }

    private async Task ExecuteBitsAndBytesAsync(
        IPyVenvRunner venvRunner,
        PyVersion pyVersion,
        IProgress<ProgressReport>? progress
    )
    {
        EnsureRocmCompatibility();

        if (pyVersion.Major != 3 || pyVersion.Minor != 12)
        {
            throw new InvalidOperationException(
                $"Windows ROCm bitsandbytes is only supported on Python 3.12.x (detected version: {pyVersion})."
            );
        }

        progress?.Report(
            new ProgressReport(-1f, "Installing bitsandbytes for Windows ROCm...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(BitsAndBytesWheelUrl).ConfigureAwait(false);
    }

    private async Task ExecuteFlashAttentionAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress
    )
    {
        EnsureRocmCompatibility();

        progress?.Report(
            new ProgressReport(
                -1f,
                "Installing Flash Attention dependencies for Windows ROCm...",
                isIndeterminate: true
            )
        );
        await venvRunner.PipInstall(AmdAiterWheelUrl).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, "Installing Flash Attention for Windows ROCm...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(FlashAttentionWheelUrl).ConfigureAwait(false);
    }

    private async Task DownloadAndReplaceFileAsync(
        DirectoryPath sageAttentionDir,
        string fileName,
        string sourceUrl,
        IProgress<ProgressReport>? progress
    )
    {
        var targetFile = sageAttentionDir.JoinFile(fileName);
        if (!targetFile.Exists)
        {
            throw new FileNotFoundException(
                $"Expected SageAttention file '{fileName}' was not found.",
                targetFile.FullPath
            );
        }

        var backupFile = sageAttentionDir.JoinFile($"{fileName}.bak");
        if (!backupFile.Exists)
        {
            await backupFile
                .WriteAllTextAsync(await targetFile.ReadAllTextAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        var tempFile = WorkingDirectory.JoinFile($"sm-rocm-sage-{fileName}.tmp");
        await downloadService.DownloadToFileAsync(sourceUrl, tempFile, progress).ConfigureAwait(false);

        try
        {
            var replacementContent = await tempFile.ReadAllTextAsync().ConfigureAwait(false);
            await targetFile.WriteAllTextAsync(replacementContent).ConfigureAwait(false);
        }
        finally
        {
            if (tempFile.Exists)
            {
                await tempFile.DeleteAsync().ConfigureAwait(false);
            }
        }
    }
}
