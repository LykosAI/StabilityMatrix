using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Semver;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class PythonPackagesItemViewModel : ViewModelBase
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
            CanUpgrade = false;
            CanDowngrade = false;
            return;
        }

        var precedence = selectedSemver.ComparePrecedenceTo(currentSemver);

        CanUpgrade = precedence > 0;
        CanDowngrade = precedence < 0;
    }

    /// <summary>
    /// Loads the pip show result if not already loaded
    /// </summary>
    public async Task LoadExtraInfo(DirectoryPath venvPath)
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
                await using var venvRunner = new PyVenvRunner(venvPath);

                PipShowResult = await venvRunner.PipShow(Package.Name);

                if (await venvRunner.PipIndex(Package.Name) is { } pipIndexResult)
                {
                    AvailableVersions = pipIndexResult.AvailableVersions;
                    SelectedVersion = Package.Version;
                }
            }
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
