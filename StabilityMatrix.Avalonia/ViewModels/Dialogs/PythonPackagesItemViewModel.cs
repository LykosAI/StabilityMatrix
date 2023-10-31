using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class PythonPackagesItemViewModel : ViewModelBase
{
    public PipPackageInfo Package { get; init; }

    [ObservableProperty]
    private PipShowResult? pipShowResult;

    [ObservableProperty]
    private bool isLoading;

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
    }
}
