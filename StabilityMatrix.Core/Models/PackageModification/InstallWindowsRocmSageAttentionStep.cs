using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallWindowsRocmSageAttentionStep(
    IDownloadService downloadService,
    IPyInstallationManager pyInstallationManager,
    IPrerequisiteHelper prerequisiteHelper,
    IRocmPackageHelper rocmPackageHelper
) : IPackageStep
{
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
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public string ProgressTitle => "Installing Windows ROCm SageAttention";

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!global::System.OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows ROCm SageAttention installation is only supported on Windows."
            );
        }

        if (!prerequisiteHelper.IsVcBuildToolsInstalled)
        {
            await prerequisiteHelper
                .InstallPackageRequirements([PackagePrerequisite.VcBuildTools], progress: progress)
                .ConfigureAwait(false);
        }

        var compatibility = rocmPackageHelper.GetCompatibility(ComfyWindowsRocmProfile.Profile);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(
                compatibility.FailureReason
                    ?? "Windows ROCm SageAttention requires a supported Windows ROCm machine state."
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

        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException(
                "torch is not installed in this ComfyUI environment. Install the Windows ROCm torch build first."
            );
        }

        if (!RocmPackageHelper.IsUsableWindowsNativeTorchBuild(torchInfo.Version, null))
        {
            throw new InvalidOperationException(
                $"Installed torch is not a usable Windows ROCm build (detected version: {torchInfo.Version})."
            );
        }

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
