using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallNunchakuStep(IPyInstallationManager pyInstallationManager) : IPackageStep
{
    public required InstalledPackage InstalledPackage { get; init; }
    public required DirectoryPath WorkingDirectory { get; init; }
    public required GpuInfo? PreferredGpu { get; init; }
    public required IPackageExtensionManager ComfyExtensionManager { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (Compat.IsMacOS || PreferredGpu?.ComputeCapabilityValue is null or < 7.5m)
        {
            throw new NotSupportedException(
                "Nunchaku is not supported on macOS or GPUs with compute capability < 7.5."
            );
        }

        var venvDir = WorkingDirectory.JoinDir("venv");
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
        var shortPythonVersionString = pyVersion.Minor switch
        {
            10 => "cp310",
            11 => "cp311",
            12 => "cp312",
            13 => "cp313",
            _ => throw new ArgumentOutOfRangeException("Invalid Python version"),
        };
        var platform = Compat.IsWindows ? "win_amd64" : "linux_x86_64";

        if (torchInfo is null)
        {
            throw new InvalidOperationException("Torch is not installed in the virtual environment.");
        }

        var torchVersion = torchInfo.Version switch
        {
            var v when v.StartsWith("2.7") => "2.7",
            var v when v.StartsWith("2.8") => "2.8",
            var v when v.StartsWith("2.9") => "2.9",
            var v when v.StartsWith("2.10") => "2.10",
            _ => throw new InvalidOperationException(
                "No compatible torch version found in the virtual environment."
            ),
        };

        var downloadUrl =
            $"https://github.com/nunchaku-tech/nunchaku/releases/download/v1.0.2/nunchaku-1.0.2+torch{torchVersion}-{shortPythonVersionString}-{shortPythonVersionString}-{platform}.whl";
        progress?.Report(
            new ProgressReport(-1f, message: "Installing Nunchaku backend", isIndeterminate: true)
        );

        await venvRunner.PipInstall(downloadUrl, progress.AsProcessOutputHandler()).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(1f, message: "Nunchaku backend installed successfully", isIndeterminate: false)
        );

        var extensions = await ComfyExtensionManager
            .GetManifestExtensionsAsync(ComfyExtensionManager.DefaultManifests)
            .ConfigureAwait(false);
        var nunchakuExtension = extensions.FirstOrDefault(e =>
            e.Title.Equals("ComfyUI-nunchaku", StringComparison.OrdinalIgnoreCase)
        );

        if (nunchakuExtension is null)
            return;

        var installedExtensions = await ComfyExtensionManager
            .GetInstalledExtensionsLiteAsync(InstalledPackage)
            .ConfigureAwait(false);
        var installedNunchakuExtension = installedExtensions.FirstOrDefault(e =>
            e.Title.Equals("ComfyUI-nunchaku", StringComparison.OrdinalIgnoreCase)
        );

        if (installedNunchakuExtension is not null)
        {
            var installedNunchakuExtensionWithVersion = await ComfyExtensionManager
                .GetInstalledExtensionInfoAsync(installedNunchakuExtension)
                .ConfigureAwait(false);
            installedNunchakuExtensionWithVersion = installedNunchakuExtensionWithVersion with
            {
                Definition = nunchakuExtension,
            };

            await ComfyExtensionManager
                .UpdateExtensionAsync(installedNunchakuExtensionWithVersion, InstalledPackage, null, progress)
                .ConfigureAwait(false);
        }
        else
        {
            await ComfyExtensionManager
                .InstallExtensionAsync(nunchakuExtension, InstalledPackage, null, progress)
                .ConfigureAwait(false);
        }

        progress?.Report(
            new ProgressReport(
                1f,
                message: "Nunchaku extension installed successfully.",
                isIndeterminate: false
            )
        );
    }

    public string ProgressTitle => "Installing nunchaku...";
}
