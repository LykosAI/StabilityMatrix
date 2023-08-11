using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public partial class PromptCardViewModel : LoadableViewModelBase
{
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty] private int editorFontSize = 14;
    
    [ObservableProperty] private string editorFontFamily = "Consolas";
}
