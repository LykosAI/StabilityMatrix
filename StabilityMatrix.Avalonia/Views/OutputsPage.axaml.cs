using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<OutputsPage>]
public partial class OutputsPage : UserControlBase
{
    public OutputsPage()
    {
        InitializeComponent();
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is OutputsPageViewModel viewModel)
        {
            viewModel.ClearSearchQuery();
        }
    }
}
