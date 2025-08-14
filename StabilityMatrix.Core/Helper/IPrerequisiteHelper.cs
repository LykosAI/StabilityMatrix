using System.Diagnostics;
using System.Runtime.Versioning;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Helper;

public interface IPrerequisiteHelper
{
    string GitBinPath { get; }

    bool IsPythonInstalled { get; }
    bool IsVcBuildToolsInstalled { get; }
    bool IsHipSdkInstalled { get; }

    Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallUvIfNecessary(IProgress<ProgressReport>? progress = null);
    string UvExePath { get; }
    bool IsUvInstalled { get; }
    Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null);

    [SupportedOSPlatform("Windows")]
    Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Run embedded git with the given arguments.
    /// </summary>
    Task RunGit(
        ProcessArgs args,
        Action<ProcessOutput>? onProcessOutput = null,
        string? workingDirectory = null
    );

    Task<ProcessResult> GetGitOutput(ProcessArgs args, string? workingDirectory = null);

    async Task<bool> CheckIsGitRepository(string repositoryPath)
    {
        var result = await GetGitOutput(["rev-parse", "--is-inside-work-tree"], repositoryPath)
            .ConfigureAwait(false);

        return result.ExitCode == 0 && result.StandardOutput?.Trim().ToLowerInvariant() == "true";
    }

    async Task<GitVersion> GetGitRepositoryVersion(string repositoryPath)
    {
        var version = new GitVersion();

        // Get tag
        if (
            await GetGitOutput(["describe", "--tags", "--abbrev=0"], repositoryPath).ConfigureAwait(false) is
            { IsSuccessExitCode: true } tagResult
        )
        {
            version = version with { Tag = tagResult.StandardOutput?.Trim() };
        }

        // Get branch
        if (
            await GetGitOutput(["rev-parse", "--abbrev-ref", "HEAD"], repositoryPath).ConfigureAwait(false) is
            { IsSuccessExitCode: true } branchResult
        )
        {
            version = version with { Branch = branchResult.StandardOutput?.Trim() };
        }

        // Get commit sha
        if (
            await GetGitOutput(["rev-parse", "HEAD"], repositoryPath).ConfigureAwait(false) is
            { IsSuccessExitCode: true } shaResult
        )
        {
            version = version with { CommitSha = shaResult.StandardOutput?.Trim() };
        }

        return version;
    }

    async Task CloneGitRepository(
        string rootDir,
        string repositoryUrl,
        GitVersion? version = null,
        Action<ProcessOutput>? onProcessOutput = null,
        string? destinationDir = null
    )
    {
        // Decide shallow clone only when not pinning to arbitrary commit post-clone
        var isShallowOk = version is null || version.Tag is not null;

        var cloneArgs = new ProcessArgsBuilder("clone");
        if (isShallowOk)
        {
            cloneArgs = cloneArgs.AddArgs("--depth", "1", "--single-branch");
        }

        if (!string.IsNullOrWhiteSpace(version?.Tag))
        {
            cloneArgs = cloneArgs.AddArgs("--branch", version.Tag!);
        }
        else if (!string.IsNullOrWhiteSpace(version?.Branch))
        {
            cloneArgs = cloneArgs.AddArgs("--branch", version.Branch!);
        }

        cloneArgs = cloneArgs.AddArg(repositoryUrl);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            cloneArgs = cloneArgs.AddArg(destinationDir);
        }

        await RunGit(cloneArgs.ToProcessArgs(), onProcessOutput, rootDir).ConfigureAwait(false);

        // If pinning to a specific commit, we need a destination directory to continue
        if (!string.IsNullOrWhiteSpace(version?.CommitSha))
        {
            if (string.IsNullOrWhiteSpace(destinationDir))
            {
                throw new InvalidOperationException(
                    "Destination directory required when checking out a specific commit."
                );
            }

            await RunGit(
                    ["fetch", "--depth", "1", "origin", version.CommitSha!],
                    onProcessOutput,
                    destinationDir
                )
                .ConfigureAwait(false);
            await RunGit(["checkout", "--force", version.CommitSha!], onProcessOutput, destinationDir)
                .ConfigureAwait(false);
            await RunGit(
                    ["submodule", "update", "--init", "--recursive", "--depth", "1"],
                    onProcessOutput,
                    destinationDir
                )
                .ConfigureAwait(false);
        }
    }

    async Task UpdateGitRepository(
        string repositoryDir,
        string repositoryUrl,
        GitVersion version,
        Action<ProcessOutput>? onProcessOutput = null,
        bool usePrune = false,
        bool allowRebaseFallback = true,
        bool allowResetHardFallback = false
    )
    {
        if (!Directory.Exists(Path.Combine(repositoryDir, ".git")))
        {
            await RunGit(["init"], onProcessOutput, repositoryDir).ConfigureAwait(false);
            await RunGit(["remote", "add", "origin", repositoryUrl], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);
        }

        // Ensure origin url matches the expected one
        await RunGit(["remote", "set-url", "origin", repositoryUrl], onProcessOutput, repositoryDir)
            .ConfigureAwait(false);

        // Specify Tag
        if (version.Tag is not null)
        {
            await RunGit(["fetch", "--tags", "--force"], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);
            await RunGit(["checkout", version.Tag, "--force"], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);
            // Update submodules
            await RunGit(["submodule", "update", "--init", "--recursive"], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);
        }
        // Specify Branch + CommitSha
        else if (version.Branch is not null && version.CommitSha is not null)
        {
            await RunGit(["fetch", "--force", "origin", version.CommitSha], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);

            await RunGit(["checkout", "--force", version.CommitSha], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);
            // Update submodules
            await RunGit(
                    ["submodule", "update", "--init", "--recursive", "--depth", "1"],
                    onProcessOutput,
                    repositoryDir
                )
                .ConfigureAwait(false);
        }
        // Specify Branch (Use latest commit)
        else if (version.Branch is not null)
        {
            // Fetch (optional prune)
            var fetchArgs = new ProcessArgsBuilder("fetch", "--force");
            if (usePrune)
                fetchArgs = fetchArgs.AddArg("--prune");
            fetchArgs = fetchArgs.AddArg("origin");
            await RunGit(fetchArgs.ToProcessArgs(), onProcessOutput, repositoryDir).ConfigureAwait(false);

            // Checkout
            await RunGit(["checkout", "--force", version.Branch], onProcessOutput, repositoryDir)
                .ConfigureAwait(false);

            // Try ff-only first
            var ffOnlyResult = await GetGitOutput(
                    ["pull", "--ff-only", "--autostash", "origin", version.Branch],
                    repositoryDir
                )
                .ConfigureAwait(false);

            if (ffOnlyResult.ExitCode != 0)
            {
                if (allowRebaseFallback)
                {
                    var rebaseResult = await GetGitOutput(
                            ["pull", "--rebase", "--autostash", "origin", version.Branch],
                            repositoryDir
                        )
                        .ConfigureAwait(false);

                    rebaseResult.EnsureSuccessExitCode();
                }
                else if (allowResetHardFallback)
                {
                    await RunGit(
                            ["fetch", "--force", "origin", version.Branch],
                            onProcessOutput,
                            repositoryDir
                        )
                        .ConfigureAwait(false);
                    await RunGit(
                            ["reset", "--hard", $"origin/{version.Branch}"],
                            onProcessOutput,
                            repositoryDir
                        )
                        .ConfigureAwait(false);
                }
                else
                {
                    ffOnlyResult.EnsureSuccessExitCode();
                }
            }

            // Update submodules
            await RunGit(
                    ["submodule", "update", "--init", "--recursive", "--depth", "1"],
                    onProcessOutput,
                    repositoryDir
                )
                .ConfigureAwait(false);
        }
        // Not specified
        else
        {
            throw new ArgumentException(
                "Version must have a tag, branch + commit sha, or branch only.",
                nameof(version)
            );
        }
    }

    Task<ProcessResult> GetGitRepositoryRemoteOriginUrl(string repositoryPath)
    {
        return GetGitOutput(["config", "--get", "remote.origin.url"], repositoryPath);
    }

    Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null);
    Task RunNpm(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null
    );
    Task InstallNodeIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallPackageRequirements(
        BasePackage package,
        PyVersion? pyVersion = null,
        IProgress<ProgressReport>? progress = null
    );
    Task InstallPackageRequirements(
        List<PackagePrerequisite> prerequisites,
        PyVersion? pyVersion = null,
        IProgress<ProgressReport>? progress = null
    );

    Task InstallDotnetIfNecessary(IProgress<ProgressReport>? progress = null);

    Task<Process> RunDotnet(
        ProcessArgs args,
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        IReadOnlyDictionary<string, string>? envVars = null,
        bool waitForExit = true
    );

    Task<bool> FixGitLongPaths();
    Task AddMissingLibsToVenv(
        DirectoryPath installedPackagePath,
        PyBaseInstall baseInstall,
        IProgress<ProgressReport>? progress = null
    );
    Task InstallPythonIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null);
    Task InstallVirtualenvIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null);
    Task InstallTkinterIfNecessary(PyVersion version, IProgress<ProgressReport>? progress = null);
}
