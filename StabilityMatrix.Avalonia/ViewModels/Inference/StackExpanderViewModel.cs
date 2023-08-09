using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackExpander))]
public partial class StackExpanderViewModel : StackCardViewModel
{
    [ObservableProperty] private string? title;
    [ObservableProperty] private bool isEnabled;
}
