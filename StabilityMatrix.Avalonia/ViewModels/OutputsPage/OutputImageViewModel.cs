using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.OutputsPage;

public partial class OutputImageViewModel : ViewModelBase
{
    public LocalImageFile ImageFile { get; }

    [ObservableProperty]
    private bool isSelected;

    public OutputImageViewModel(LocalImageFile imageFile)
    {
        ImageFile = imageFile;
    }
}
