using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using NLog;
using StabilityMatrix.Avalonia.Controls.TextMarkers;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Behaviors;

/// <summary>
/// Underlines prompt errors in a <see cref="TextEditor"/> while typing, and shows the error on hover.
/// Re-checks when the model index changes, so installing a missing model clears its error.
/// </summary>
public class TextEditorDiagnosticsBehavior : Behavior<TextEditor>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly TimeSpan ValidationDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer validationTimer = new() { Interval = ValidationDelay };

    private TextEditor? textEditor;
    private TextDocument? document;
    private TextMarkerService? markerService;
    private bool isToolTipOpen;

    public static readonly StyledProperty<PromptDiagnosticsProvider?> DiagnosticsProviderProperty =
        AvaloniaProperty.Register<TextEditorDiagnosticsBehavior, PromptDiagnosticsProvider?>(
            nameof(DiagnosticsProvider)
        );

    public PromptDiagnosticsProvider? DiagnosticsProvider
    {
        get => GetValue(DiagnosticsProviderProperty);
        set => SetValue(DiagnosticsProviderProperty, value);
    }

    public TextEditorDiagnosticsBehavior()
    {
        validationTimer.Tick += (_, _) =>
        {
            validationTimer.Stop();
            Validate();
        };
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        textEditor = AssociatedObject ?? throw new NullReferenceException("AssociatedObject is null");

        textEditor.TextArea.DocumentChanged += OnDocumentChanged;
        textEditor.PointerHover += OnPointerHover;
        textEditor.PointerHoverStopped += OnPointerHoverStopped;
        textEditor.AttachedToVisualTree += OnEditorAttachedToVisualTree;
        textEditor.DetachedFromVisualTree += OnEditorDetachedFromVisualTree;

        if (textEditor.GetVisualRoot() is not null)
        {
            EventManager.Instance.ModelIndexChanged += OnModelIndexChanged;
        }

        AttachDocument(textEditor.Document);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        validationTimer.Stop();
        EventManager.Instance.ModelIndexChanged -= OnModelIndexChanged;

        if (textEditor is not null)
        {
            textEditor.TextArea.DocumentChanged -= OnDocumentChanged;
            textEditor.PointerHover -= OnPointerHover;
            textEditor.PointerHoverStopped -= OnPointerHoverStopped;
            textEditor.AttachedToVisualTree -= OnEditorAttachedToVisualTree;
            textEditor.DetachedFromVisualTree -= OnEditorDetachedFromVisualTree;
        }

        AttachDocument(null);
        textEditor = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DiagnosticsProviderProperty || change.Property == IsEnabledProperty)
        {
            Validate();
        }
    }

    private void AttachDocument(TextDocument? newDocument)
    {
        if (document is not null)
        {
            document.TextChanged -= OnTextChanged;
        }

        if (markerService is not null)
        {
            textEditor?.TextArea.TextView.BackgroundRenderers.Remove(markerService);
            markerService = null;
        }

        document = newDocument;

        if (textEditor is null || newDocument is null)
        {
            return;
        }

        markerService = new TextMarkerService(textEditor);
        textEditor.TextArea.TextView.BackgroundRenderers.Add(markerService);
        newDocument.TextChanged += OnTextChanged;

        Validate();
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => AttachDocument(textEditor?.Document);

    private void OnTextChanged(object? sender, EventArgs e) => ScheduleValidation();

    private void OnModelIndexChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ScheduleValidation);

    private void OnEditorAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Static event, only held while visible so a closed tab's editor can be collected
        EventManager.Instance.ModelIndexChanged += OnModelIndexChanged;
        // The index may have changed while we weren't listening
        ScheduleValidation();
    }

    private void OnEditorDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EventManager.Instance.ModelIndexChanged -= OnModelIndexChanged;
        validationTimer.Stop();
    }

    private void ScheduleValidation()
    {
        validationTimer.Stop();
        validationTimer.Start();
    }

    private void Validate()
    {
        Dispatcher.UIThread.VerifyAccess();

        if (markerService is null || document is null)
        {
            return;
        }

        markerService.RemoveAll(_ => true);

        if (!IsEnabled || DiagnosticsProvider is not { } provider)
        {
            return;
        }

        IReadOnlyList<PromptError> diagnostics;
        try
        {
            diagnostics = provider.GetDiagnostics(document.Text);
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to get prompt diagnostics");
            return;
        }

        var color =
            textEditor is not null
            && textEditor.TryFindResource("ThemeRedColor", textEditor.ActualThemeVariant, out var value)
            && value is Color themeColor
                ? themeColor
                : Colors.Red;

        foreach (var error in diagnostics)
        {
            // Zero-width errors (like an unexpected end of text) underline the character before them
            var (start, length) =
                error.TextEndOffset > error.TextOffset
                    ? (error.TextOffset, error.TextEndOffset - error.TextOffset)
                    : (Math.Max(error.TextOffset - 1, 0), 1);

            if (markerService.TryCreate(start, length) is { } marker)
            {
                marker.MarkerColor = color;
                marker.ToolTip = error.Message;
            }
        }
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (textEditor is null || document is null || markerService is null)
        {
            return;
        }

        var textView = textEditor.TextArea.TextView;

        TextViewPosition? position;
        try
        {
            position = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        if (position is not { IsAtEndOfLine: false } hoverPosition)
        {
            return;
        }

        var offset = document.GetOffset(hoverPosition.Location);
        var message = markerService
            .GetMarkersAtOffset(offset)
            .Where(marker => offset < marker.EndOffset)
            .Select(marker => marker.ToolTip as string)
            .FirstOrDefault(tip => tip is not null);

        if (message is null)
        {
            return;
        }

        ToolTip.SetTip(
            textEditor,
            new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
            }
        );
        ToolTip.SetPlacement(textEditor, PlacementMode.Pointer);
        ToolTip.SetShowDelay(textEditor, 0);
        ToolTip.SetIsOpen(textEditor, true);
        isToolTipOpen = true;
        e.Handled = true;
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        if (!isToolTipOpen || textEditor is null)
        {
            return;
        }

        ToolTip.SetIsOpen(textEditor, false);
        ToolTip.SetTip(textEditor, null);
        isToolTipOpen = false;
    }
}
