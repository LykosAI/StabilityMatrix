using System.Diagnostics;
using System.Runtime.Versioning;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Helper;

public interface IPrerequisiteHelper
{
    string GitBinPath { get; }

    bool IsPythonInstalled { get; }

    Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null);
    Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null);

    [SupportedOSPlatform("Windows")]
    Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Run embedded git with the given arguments.
    /// </summary>
    Task RunGit(ProcessArgs args, Action<ProcessOutput>? onProcessOutput, string? workingDirectory = null);

    /// <summary>
    /// Run embedded git with the given arguments.
    /// </summary>
    Task RunGit(ProcessArgs args, string? workingDirectory = null);

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

    async Task CloneGitRepository(string rootDir, string repositoryUrl, GitVersion? version = null)
    {
        // Latest if no version is given
        if (version is null)
        {
            await RunGit(["clone", "--depth", "1", repositoryUrl], rootDir).ConfigureAwait(false);
        }
        else if (version.Tag is not null)
        {
            await RunGit(["clone", "--depth", "1", version.Tag, repositoryUrl], rootDir)
                .ConfigureAwait(false);
        }
        else if (version.Branch is not null && version.CommitSha is not null)
        {
            await RunGit(["clone", "--depth", "1", "--branch", version.Branch, repositoryUrl], rootDir)
                .ConfigureAwait(false);

            await RunGit(["checkout", version.CommitSha, "--force"], rootDir).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("Version must have a tag or branch and commit sha.", nameof(version));
        }
    }

    async Task UpdateGitRepository(string repositoryDir, string repositoryUrl, GitVersion version)
    {
        // Specify Tag
        if (version.Tag is not null)
        {
            await RunGit(["init"], repositoryDir).ConfigureAwait(false);
            await RunGit(["remote", "add", "origin", repositoryUrl], repositoryDir).ConfigureAwait(false);
            await RunGit(["fetch", "--tags"], repositoryDir).ConfigureAwait(false);

            await RunGit(["checkout", version.Tag, "--force"], repositoryDir).ConfigureAwait(false);
            // Update submodules
            await RunGit(["submodule", "update", "--init", "--recursive"], repositoryDir)
                .ConfigureAwait(false);
        }
        // Specify Branch + CommitSha
        else if (version.Branch is not null && version.CommitSha is not null)
        {
            await RunGit(["init"], repositoryDir).ConfigureAwait(false);
            await RunGit(["remote", "add", "origin", repositoryUrl], repositoryDir).ConfigureAwait(false);
            await RunGit(["fetch", "--tags"], repositoryDir).ConfigureAwait(false);

            await RunGit(["checkout", version.CommitSha, "--force"], repositoryDir).ConfigureAwait(false);
            // Update submodules
            await RunGit(["submodule", "update", "--init", "--recursive"], repositoryDir)
                .ConfigureAwait(false);
        }
        // Specify Branch (Use latest commit)
        else if (version.Branch is not null)
        {
            // Fetch
            await RunGit(["fetch", "--tags", "--force"], repositoryDir).ConfigureAwait(false);
            // Checkout
            await RunGit(["checkout", version.Branch, "--force"], repositoryDir).ConfigureAwait(false);
            // Pull latest
            await RunGit(["pull", "--autostash", "origin", version.Branch], repositoryDir)
                .ConfigureAwait(false);
            // Update submodules
            await RunGit(["submodule", "update", "--init", "--recursive"], repositoryDir)
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
    Task InstallPackageRequirements(BasePackage package, IProgress<ProgressReport>? progress = null);
    Task InstallPackageRequirements(
        List<PackagePrerequisite> prerequisites,
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
}
