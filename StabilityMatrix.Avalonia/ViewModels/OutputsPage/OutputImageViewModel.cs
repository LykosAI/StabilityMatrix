using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.OutputsPage;

public class OutputImageViewModel : SelectableViewModelBase
{
    public OutputImageViewModel(LocalImageFile imageFile)
    {
        ImageFile = imageFile;
    }

    public LocalImageFile ImageFile { get; }
}
