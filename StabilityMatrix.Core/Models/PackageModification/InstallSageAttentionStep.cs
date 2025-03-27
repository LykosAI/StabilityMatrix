using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallSageAttentionStep(
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : IPackageStep
{
    private const string PythonLibsDownloadUrl = "https://cdn.lykos.ai/python_libs_for_sage.zip";

    public required InstalledPackage InstalledPackage { get; init; }
    public required DirectoryPath WorkingDirectory { get; init; }
    public required bool IsBlackwellGpu { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!Compat.IsWindows)
        {
            throw new PlatformNotSupportedException(
                "This method of installing Triton and SageAttention is only supported on Windows"
            );
        }

        if (!prerequisiteHelper.IsVcBuildToolsInstalled)
        {
            throw new MissingPrerequisiteException(
                "Visual Studio 2022 Build Tools",
                "Could not find Visual Studio 2022 Build Tools. Please install them from the link below.",
                "https://aka.ms/vs/17/release/vs_BuildTools.exe"
            );
        }

        var nvccPath = await Utilities.WhichAsync("nvcc").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(nvccPath))
        {
            var cuda126ExpectedPath = new DirectoryPath(
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6\bin"
            );
            var cuda128ExpectedPath = new DirectoryPath(
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8\bin"
            );

            if (!cuda126ExpectedPath.Exists && !cuda128ExpectedPath.Exists)
            {
                throw new MissingPrerequisiteException(
                    "CUDA Toolkit",
                    "Could not find CUDA Toolkit. Please install version 12.6 or newer from the link below.",
                    "https://developer.nvidia.com/cuda-downloads?target_os=Windows&target_arch=x86_64"
                );
            }

            nvccPath = cuda128ExpectedPath.Exists
                ? cuda128ExpectedPath.JoinFile("nvcc.exe").ToString()
                : cuda126ExpectedPath.JoinFile("nvcc.exe").ToString();
        }

        var venvDir = WorkingDirectory.JoinDir("venv");

        await using var venvRunner = PyBaseInstall.Default.CreateVenvRunner(
            venvDir,
            workingDirectory: WorkingDirectory,
            environmentVariables: EnvironmentVariables
        );

        venvRunner.UpdateEnvironmentVariables(env =>
        {
            var cudaBinPath = Path.GetDirectoryName(nvccPath)!;
            var cudaHome = Path.GetDirectoryName(cudaBinPath)!;

            env = env.TryGetValue("PATH", out var pathValue)
                ? env.SetItem("PATH", $"{cudaBinPath}{Path.PathSeparator}{pathValue}")
                : env.Add("PATH", cudaBinPath);

            if (!env.ContainsKey("CUDA_HOME"))
            {
                env = env.Add("CUDA_HOME", cudaHome);
            }

            return env;
        });

        progress?.Report(new ProgressReport(-1f, message: "Installing Triton", isIndeterminate: true));

        var pipArgs = new PipInstallArgs();
        if (IsBlackwellGpu)
        {
            pipArgs = pipArgs.AddArg("--pre");
        }
        pipArgs = pipArgs.AddArg("triton-windows");

        await venvRunner.PipInstall(pipArgs, progress.AsProcessOutputHandler()).ConfigureAwait(false);

        venvRunner.UpdateEnvironmentVariables(env => env.SetItem("SETUPTOOLS_USE_DISTUTILS", "setuptools"));

        progress?.Report(
            new ProgressReport(-1f, message: "Downloading Python libraries", isIndeterminate: true)
        );
        var downloadPath = WorkingDirectory.JoinFile("python_libs_for_sage.zip");
        await downloadService
            .DownloadToFileAsync(PythonLibsDownloadUrl, downloadPath, progress)
            .ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, message: "Extracting Python libraries", isIndeterminate: true)
        );
        await ArchiveHelper.Extract7Z(downloadPath, venvDir, progress).ConfigureAwait(false);

        var includeFolder = venvDir.JoinDir("include");
        var scriptsIncludeFolder = venvDir.JoinDir("Scripts").JoinDir("include");
        await includeFolder.CopyToAsync(scriptsIncludeFolder).ConfigureAwait(false);

        var sageDir = WorkingDirectory.JoinDir("SageAttention");

        if (!sageDir.Exists)
        {
            progress?.Report(
                new ProgressReport(-1f, message: "Downloading SageAttention", isIndeterminate: true)
            );
            await prerequisiteHelper
                .RunGit(["clone", "https://github.com/thu-ml/SageAttention.git", sageDir.ToString()])
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1f, message: "Installing SageAttention", isIndeterminate: true));
        await venvRunner
            .PipInstall(
                [WorkingDirectory.JoinDir("SageAttention").ToString()],
                progress.AsProcessOutputHandler()
            )
            .ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing Triton and SageAttention";
}
