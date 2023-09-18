using AvaloniaEdit;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.Avalonia.Controls;

public static class EditorCommands
{
    public static RelayCommand<TextEditor> CopyCommand { get; } =
        new(editor => editor?.Copy(), editor => editor?.CanCopy ?? false);

    public static RelayCommand<TextEditor> CutCommand { get; } =
        new(editor => editor?.Cut(), editor => editor?.CanCut ?? false);

    public static RelayCommand<TextEditor> PasteCommand { get; } =
        new(editor => editor?.Paste(), editor => editor?.CanPaste ?? false);
}
