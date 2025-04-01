using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace StabilityMatrix.Avalonia.Extensions;

public static class AvaloniaEditExtensions
{
    public static ISegment? GetSelectionSegment(this TextEditor editor)
    {
        if (editor.SelectionLength == 0)
            return null;

        return new SimpleSegment(editor.SelectionStart, editor.SelectionLength);
    }
}
