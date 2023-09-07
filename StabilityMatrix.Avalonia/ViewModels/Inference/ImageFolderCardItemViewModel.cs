using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

public partial class ImageFolderCardItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private LocalImageFile? localImageFile;

    [ObservableProperty]
    private string? imagePath;
}
