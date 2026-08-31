using System;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<DocumentationPage>]
public partial class DocumentationPage : UserControlBase
{
    private DocumentationViewModel? subscribedViewModel;

    public DocumentationPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.AnchorRequested -= OnAnchorRequested;
        }

        subscribedViewModel = DataContext as DocumentationViewModel;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.AnchorRequested += OnAnchorRequested;
        }
    }

    private void OnAnchorRequested(object? sender, string anchor)
    {
        // Forward the anchor request to the markdown viewer (keeps the VM free of control refs).
        MarkdownViewer?.ScrollToAnchor(anchor);
    }
}
