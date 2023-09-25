using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Controls;

public partial class AdvancedImageBoxView : UserControl
{
    public AdvancedImageBoxView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        var copyMenuItem = this.FindControl<MenuFlyoutItem>("CopyMenuItem")!;
        copyMenuItem.Command = new AsyncRelayCommand<Bitmap?>(FlyoutCopy);
    }
    
    private static async Task FlyoutCopy(Bitmap? image)
    {
        if (image is null || !Compat.IsWindows) return;

        await Task.Run(() =>
        {
            if (Compat.IsWindows)
            {
                WindowsClipboard.SetBitmap(image);
            }
        });
    }
}
