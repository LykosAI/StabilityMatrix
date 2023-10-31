using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
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
            await using var venvRunner = new PyVenvRunner(venvPath);

            PipShowResult = await venvRunner.PipShow(Package.Name);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
