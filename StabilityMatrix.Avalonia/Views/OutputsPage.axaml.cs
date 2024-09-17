using Avalonia.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
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
