using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Controls;

public partial class AdvancedImageBoxView : UserControl
{
    public AdvancedImageBoxView()
    {
        InitializeComponent();
    }

    public static AsyncRelayCommand<Bitmap?> FlyoutCopyCommand { get; } = new(FlyoutCopy);

    public static async Task FlyoutCopy(Bitmap? image)
    {
        if (image is null || !Compat.IsWindows)
            return;

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap(image);
            }
        });
    }
}
