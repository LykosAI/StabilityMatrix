using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Semver;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class PythonPackagesItemViewModel(ISettingsManager settingsManager) : ViewModelBase
{
    [ObservableProperty]
    private PipPackageInfo package;

    [ObservableProperty]
    private string? selectedVersion;

    [ObservableProperty]
    private IReadOnlyList<string>? availableVersions;

    [ObservableProperty]
    private PipShowResult? pipShowResult;

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// True if selected version is newer than the installed version
    /// </summary>
    [ObservableProperty]
    private bool canUpgrade;

    /// <summary>
    /// True if selected version is older than the installed version
    /// </summary>
    [ObservableProperty]
    private bool canDowngrade;

    partial void OnSelectedVersionChanged(string? value)
    {
        if (
            value is null
            || Package.Version == value
            || !SemVersion.TryParse(Package.Version, out var currentSemver)
            || !SemVersion.TryParse(value, out var selectedSemver)
        )
        {
            var compare = string.CompareOrdinal(value, Package.Version);
            CanUpgrade = compare > 0;
            CanDowngrade = compare < 0;
            return;
        }

        var precedence = selectedSemver.ComparePrecedenceTo(currentSemver);

        CanUpgrade = precedence > 0;
        CanDowngrade = precedence < 0;
    }

    /// <summary>
    /// Return the known index URL for a package, currently this is torch, torchvision and torchaudio
    /// </summary>
    public static string? GetKnownIndexUrl(string packageName, string version)
    {
        var torchPackages = new[] { "torch", "torchvision", "torchaudio" };
        if (torchPackages.Contains(packageName) && version.Contains('+'))
        {
            // Get the metadata for the current version (everything after the +)
            var indexName = version.Split('+', 2).Last();

            var indexUrl = $"https://download.pytorch.org/whl/{indexName}";
            return indexUrl;
        }

        return null;
    }

    /// <summary>
    /// Loads the pip show result if not already loaded
    /// </summary>
    public async Task LoadExtraInfo(DirectoryPath venvPath, PyBaseInstall pyBaseInstall)
    {
        if (PipShowResult is not null)
        {
            return;
        }

        IsLoading = true;

        try
        {
            if (Design.IsDesignMode)
            {
                await LoadExtraInfoDesignMode();
            }
            else
            {
                await using var venvRunner = await pyBaseInstall.CreateVenvRunnerAsync(
                    venvPath,
                    workingDirectory: venvPath.Parent,
                    environmentVariables: settingsManager.Settings.EnvironmentVariables
                );

                PipShowResult = await venvRunner.PipShow(Package.Name);

                // Attempt to get known index url
                var indexUrl = GetKnownIndexUrl(Package.Name, Package.Version);

                if (await venvRunner.PipIndex(Package.Name, indexUrl) is { } pipIndexResult)
                {
                    AvailableVersions = pipIndexResult.AvailableVersions;
                    SelectedVersion = Package.Version;
                }
            }
        }
        catch (ProcessException)
        {
            // Ignore
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadExtraInfoDesignMode()
    {
        await using var _ = new MinimumDelay(200, 300);

        PipShowResult = new PipShowResult { Name = Package.Name, Version = Package.Version };
        AvailableVersions = new[] { Package.Version, "1.2.0", "1.1.0", "1.0.0" };
        SelectedVersion = Package.Version;
    }
}
