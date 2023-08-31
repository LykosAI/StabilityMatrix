using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using StabilityMatrix.Avalonia.Helpers;

namespace StabilityMatrix.Avalonia.Controls;

public class PromptCard : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        InitializeEditors(e);
    }

    private static void InitializeEditors(TemplateAppliedEventArgs e)
    {
        foreach (
            var editor in new[]
            {
                e.NameScope.Find<TextEditor>("PromptEditor"),
                e.NameScope.Find<TextEditor>("NegativePromptEditor")
            }
        )
        {
            if (editor is not null)
            {
                TextEditorConfigs.ConfigForPrompt(editor);
            }
        }
    }
}
