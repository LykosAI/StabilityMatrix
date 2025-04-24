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

    public static RelayCommand<TextEditor> UndoCommand { get; } =
        new(editor => editor?.Undo(), editor => editor?.CanUndo ?? false);

    public static RelayCommand<TextEditor> RedoCommand { get; } =
        new(editor => editor?.Redo(), editor => editor?.CanRedo ?? false);
}
