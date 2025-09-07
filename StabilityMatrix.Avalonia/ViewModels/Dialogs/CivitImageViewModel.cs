using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitImageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int ImageId { get; set; }

    [ObservableProperty]
    public partial ImageSource ImageSource { get; set; }
}
