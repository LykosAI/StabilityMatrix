using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class CheckpointCard : ObservableObject
{
    [ObservableProperty]
    private BitmapImage? image;
    
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string fileName;
}
