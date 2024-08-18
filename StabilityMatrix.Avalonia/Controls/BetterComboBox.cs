using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterComboBox : ComboBox
{
    public static readonly DirectProperty<BetterComboBox, IDataTemplate?> SelectionBoxItemTemplateProperty =
        AvaloniaProperty.RegisterDirect<BetterComboBox, IDataTemplate?>(
            nameof(SelectionBoxItemTemplate),
            v => v.SelectionBoxItemTemplate,
            (x, v) => x.SelectionBoxItemTemplate = v
        );

    public IDataTemplate? SelectionBoxItemTemplate
    {
        get => selectionBoxItemTemplate;
        set => SetAndRaise(SelectionBoxItemTemplateProperty, ref selectionBoxItemTemplate, value);
    }

    private IDataTemplate? selectionBoxItemTemplate;
    private readonly Subject<string> inputSubject = new();
    private readonly IDisposable subscription;
    private readonly Popup inputPopup;
    private readonly TextBlock inputTextBlock;
    private string currentInput = string.Empty;

    public BetterComboBox()
    {
        // Create an observable that buffers input over a short period
        var inputObservable = inputSubject
            .Do(text => currentInput += text)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Where(_ => !string.IsNullOrEmpty(currentInput))
            .Select(_ => currentInput);

        // Subscribe to the observable to filter the ComboBox items
        subscription = inputObservable.Subscribe(OnInputReceived, _ => ResetPopupText());

        // Initialize the popup
        inputPopup = new Popup
        {
            IsLightDismissEnabled = true,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.Bottom,
            PlacementGravity = PopupGravity.Top,
        };

        // Initialize the TextBlock with custom styling
        inputTextBlock = new TextBlock
        {
            Foreground = Brushes.White, // White text color
            Background = Brush.Parse("#333333"), // Dark gray background
            Padding = new Thickness(8), // Add padding
            FontSize = 14 // Optional: adjust font size
        };

        inputPopup.Child = inputTextBlock;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Set the Popup's anchor to the ComboBox itself
        inputPopup.PlacementTarget = this;

        if (e.NameScope.Find<ContentControl>("ContentPresenter") is { } contentPresenter)
        {
            if (SelectionBoxItemTemplate is { } template)
            {
                contentPresenter.ContentTemplate = template;
            }
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled)
            return;

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            // Push the input text to the subject
            inputSubject.OnNext(e.Text);
            UpdatePopupText(e.Text);
            e.Handled = true;
        }

        base.OnTextInput(e);
    }

    private void OnInputReceived(string input)
    {
        if (Items.OfType<Enum>().ToList() is { Count: > 0 } enumItems)
        {
            var foundEnum = enumItems.FirstOrDefault(
                x => x.GetStringValue().StartsWith(input, StringComparison.OrdinalIgnoreCase)
            );

            if (foundEnum is not null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedItem = foundEnum;
                });
            }
        }
        else if (Items.OfType<ISearchText>().ToList() is { } modelFiles)
        {
            var found = modelFiles.FirstOrDefault(
                x => x.SearchText.StartsWith(input, StringComparison.OrdinalIgnoreCase)
            );

            if (found is not null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedItem = found;
                });
            }
        }

        Dispatcher.UIThread.Post(ResetPopupText);
    }

    private void UpdatePopupText(string text)
    {
        inputTextBlock.Text += text; // Accumulate text in the popup

        if (!inputPopup.IsOpen)
        {
            inputPopup.IsOpen = true;
        }
    }

    private void ResetPopupText()
    {
        currentInput = string.Empty;
        inputTextBlock.Text = string.Empty;
        inputPopup.IsOpen = false;
    }

    // Ensure proper disposal of resources
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        subscription.Dispose();
    }
}
