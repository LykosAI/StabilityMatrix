using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<BananaVisionPage>]
public partial class BananaVisionPage : UserControlBase
{
    public BananaVisionPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Set the StorageProvider on the ViewModel
        if (DataContext is BananaVisionPageViewModel viewModel)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                viewModel.StorageProvider = topLevel.StorageProvider;
            }
        }
    }
}
