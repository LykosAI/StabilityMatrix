using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
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
                editor.TextArea.Margin = new Thickness(0, 0, 4, 0);

                if (editor.TextArea.ActiveInputHandler is TextAreaInputHandler inputHandler)
                {
                    // Add some aliases for editor shortcuts
                    inputHandler.KeyBindings.AddRange(
                        new KeyBinding[]
                        {
                            new()
                            {
                                Command = ApplicationCommands.Cut,
                                Gesture = new KeyGesture(Key.Delete, KeyModifiers.Shift)
                            },
                            new()
                            {
                                Command = ApplicationCommands.Paste,
                                Gesture = new KeyGesture(Key.Insert, KeyModifiers.Shift)
                            }
                        }
                    );
                }
            }
        }
    }
}
