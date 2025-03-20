using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
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
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!Compat.IsWindows)
        {
            throw new PlatformNotSupportedException(
                "This method of installing Triton and SageAttention is only supported on Windows"
            );
        }

        var clPath = await Utilities.WhichAsync("cl").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clPath))
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
            throw new MissingPrerequisiteException(
                "CUDA Toolkit",
                "Could not find CUDA Toolkit. Please install version 12.6 or newer from the link below.",
                "https://developer.nvidia.com/cuda-downloads?target_os=Windows&target_arch=x86_64"
            );
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

            env = env.TryGetValue("PATH", out var pathvalue)
                ? env.SetItem("PATH", $"{cudaBinPath}{Path.PathSeparator}{pathvalue}")
                : env.Add("PATH", cudaBinPath);

            if (!env.ContainsKey("CUDA_HOME"))
            {
                env = env.Add("CUDA_HOME", cudaHome);
            }

            return env;
        });

        await venvRunner
            .PipInstall("triton-windows", progress.AsProcessOutputHandler())
            .ConfigureAwait(false);

        venvRunner.UpdateEnvironmentVariables(env => env.SetItem("SETUPTOOLS_USE_DISTUTILS", "setuptools"));

        var downloadPath = WorkingDirectory.JoinFile("python_libs_for_sage.zip");
        await downloadService
            .DownloadToFileAsync(PythonLibsDownloadUrl, downloadPath, progress)
            .ConfigureAwait(false);

        await ArchiveHelper.Extract7Z(downloadPath, venvDir, progress).ConfigureAwait(false);

        var includeFolder = venvDir.JoinDir("include");
        var scriptsIncludeFolder = venvDir.JoinDir("Scripts").JoinDir("include");
        await includeFolder.CopyToAsync(scriptsIncludeFolder).ConfigureAwait(false);

        var sageDir = WorkingDirectory.JoinDir("SageAttention");

        if (!sageDir.Exists)
        {
            await prerequisiteHelper
                .RunGit(["clone", "https://github.com/thu-ml/SageAttention.git", sageDir.ToString()])
                .ConfigureAwait(false);
        }

        await venvRunner
            .PipInstall(
                WorkingDirectory.JoinDir("SageAttention").ToString(),
                progress.AsProcessOutputHandler()
            )
            .ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing Triton and SageAttention";
}
