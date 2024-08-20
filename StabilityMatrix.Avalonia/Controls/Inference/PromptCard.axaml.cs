using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class PromptCard : TemplatedControlBase
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        FixGrids(e);
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
                TextEditorConfigs.Configure(editor, TextEditorPreset.Prompt);
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

    private void FixGrids(TemplateAppliedEventArgs e)
    {
        if (DataContext is not PromptCardViewModel { IsNegativePromptEnabled: false })
        {
            return;
        }

        // When negative prompt disabled, rearrange grid
        if (e.NameScope.Find<Grid>("PART_RootGrid") is not { } rootGrid)
            return;

        // Change `*,16,*,16,Auto` to `*,16,Auto` (Remove index 2 and 3)
        rootGrid.RowDefinitions.RemoveRange(2, 2);

        // Set the last children to row 2
        rootGrid.Children[4].SetValue(Grid.RowProperty, 2);

        // Remove the negative prompt row and the separator row (index 2 and 3)
        rootGrid.Children.RemoveRange(2, 2);
    }
}
