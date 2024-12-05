using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class ConsoleViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private bool isDisposed;

    // Queue for console updates
    private BufferBlock<ProcessOutput> buffer = new();

    // Task that updates the console (runs on UI thread)
    private Task? updateTask;

    // Cancellation token source for updateTask
    private CancellationTokenSource? updateCts;

    public int MaxLines { get; set; } = -1;

    public bool IsUpdatesRunning => updateTask?.IsCompleted == false;

    [ObservableProperty]
    private TextDocument document = new();

    /// <summary>
    /// Current offset for write operations.
    /// </summary>
    private int writeCursor;

    /// <summary>
    /// Lock for accessing <see cref="writeCursor"/>
    /// </summary>
    private readonly AsyncLock writeCursorLock = new();

    /// <summary>
    /// Timeout for acquiring locks on <see cref="writeCursor"/>
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public TimeSpan WriteCursorLockTimeout { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets a cancellation token using the cursor lock timeout
    /// </summary>
    private CancellationToken WriteCursorLockTimeoutToken =>
        new CancellationTokenSource(WriteCursorLockTimeout).Token;

    /// <summary>
    /// Event invoked when an ApcMessage of type Input is received.
    /// </summary>
    public event EventHandler<ApcMessage>? ApcInput;

    /// <summary>
    /// Starts update task for processing Post messages.
    /// </summary>
    /// <exception cref="InvalidOperationException">If update task is already running</exception>
    public void StartUpdates()
    {
        if (updateTask is not null)
        {
            throw new InvalidOperationException("Update task is already running");
        }
        updateCts = new CancellationTokenSource();
        updateTask = Dispatcher.UIThread.InvokeAsync(ConsoleUpdateLoop, DispatcherPriority.Send);
    }

    /// <summary>
    /// Cancels the update task and waits for it to complete.
    /// </summary>
    public async Task StopUpdatesAsync()
    {
        Logger.Trace($"Stopping console updates, current buffer items: {buffer.Count}");
        // First complete the buffer
        buffer.Complete();
        // Wait for buffer to complete, max 3 seconds
        var completionCts = new CancellationTokenSource(3000);
        try
        {
            await buffer.Completion.WaitAsync(completionCts.Token);
        }
        catch (TaskCanceledException e)
        {
            // We can still continue since this just means we lose
            // some remaining output
            Logger.Warn("Buffer completion timed out: " + e.Message);
        }

        // Cancel update task
        updateCts?.Cancel();
        updateCts = null;

        // Wait for update task
        if (updateTask is not null)
        {
            await updateTask;
            updateTask = null;
        }
        Logger.Trace($"Stopped console updates with {buffer.Count} buffer items remaining");
    }

    /// <summary>
    /// Clears the console and sets a new buffer.
    /// This also resets the write cursor to 0.
    /// </summary>
    public async Task Clear()
    {
        // Clear document
        Document.Text = string.Empty;
        // Reset write cursor
        await ResetWriteCursor();
        // Clear buffer and create new one
        buffer.Complete();
        buffer = new BufferBlock<ProcessOutput>();
    }

    /// <summary>
    /// Resets the write cursor to be equal to the document length.
    /// </summary>
    public async Task ResetWriteCursor()
    {
        using (await writeCursorLock.LockAsync(WriteCursorLockTimeoutToken))
        {
            Logger.ConditionalTrace($"Reset cursor to end: ({writeCursor} -> {Document.TextLength})");
            writeCursor = Document.TextLength;
        }
        DebugPrintDocument();
    }

    [RelayCommand]
    private async Task CopySelection(TextEditor textEditor)
    {
        await App.Clipboard.SetTextAsync(textEditor.SelectedText);
    }

    [RelayCommand]
    private void SelectAll(TextEditor textEditor)
    {
        textEditor.SelectAll();
    }

    [Localizable(false)]
    [RelayCommand]
    private void SearchWithGoogle(TextEditor textEditor)
    {
        var url = $"https://google.com/search?q={HttpUtility.UrlEncode(textEditor.SelectedText)}";
        ProcessRunner.OpenUrl(url);
    }

    [Localizable(false)]
    [RelayCommand]
    private void SearchWithChatGpt(TextEditor textEditor)
    {
        var url = $"https://chatgpt.com/?q={HttpUtility.UrlEncode(textEditor.SelectedText)}";
        ProcessRunner.OpenUrl(url);
    }

    private async Task ConsoleUpdateLoop()
    {
        // This must be run in the UI thread
        Dispatcher.UIThread.VerifyAccess();

        // Get cancellation token
        var ct =
            updateCts?.Token ?? throw new InvalidOperationException("Update cancellation token must be set");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ProcessOutput output;

                try
                {
                    output = await buffer.ReceiveAsync(ct);
                }
                catch (InvalidOperationException e)
                {
                    // Thrown when buffer is completed, convert to OperationCanceledException
                    throw new OperationCanceledException("Update buffer completed", e);
                }

                var outputType = output.IsStdErr ? "stderr" : "stdout";
                Logger.ConditionalTrace(
                    $"Processing: [{outputType}] (Text = {output.Text.ToRepr()}, "
                        + $"Raw = {output.RawText?.ToRepr()}, "
                        + $"CarriageReturn = {output.CarriageReturn}, "
                        + $"CursorUp = {output.CursorUp}, "
                        + $"AnsiCommand = {output.AnsiCommand})"
                );

                // Link the cancellation token to the write cursor lock timeout
                var linkedCt = CancellationTokenSource
                    .CreateLinkedTokenSource(ct, WriteCursorLockTimeoutToken)
                    .Token;

                using (await writeCursorLock.LockAsync(linkedCt))
                {
                    ConsoleUpdateOne(output);
                }
            }
        }
        catch (OperationCanceledException e)
        {
            Logger.Debug($"Console update loop canceled: {e.Message}");
        }
        catch (Exception e)
        {
            // Log other errors and continue here to not crash the UI thread
            Logger.Error(e, $"Unexpected error in console update loop: {e.GetType().Name} {e.Message}");
        }
    }

    /// <summary>
    /// Handle one instance of ProcessOutput.
    /// Calls to this function must be synchronized with <see cref="writeCursorLock"/>
    /// </summary>
    /// <remarks>Not checked, but must be run in the UI thread.</remarks>
    private void ConsoleUpdateOne(ProcessOutput output)
    {
        Debug.Assert(Dispatcher.UIThread.CheckAccess());

        // Check for Apc messages
        if (output.ApcMessage is not null)
        {
            // Handle Apc message, for now just input audit events
            var message = output.ApcMessage.Value;
            if (message.Type == ApcType.Input)
            {
                ApcInput?.Invoke(this, message);
            }
            // Ignore further processing
            return;
        }

        // If we have a carriage return,
        // start current write at the beginning of the current line
        if (output.CarriageReturn > 0)
        {
            var currentLine = Document.GetLineByOffset(writeCursor);

            // Get the start of current line as new write cursor
            var lineStartOffset = currentLine.Offset;

            // See if we need to move the cursor
            if (lineStartOffset == writeCursor)
            {
                Logger.ConditionalTrace(
                    $"Cursor already at start for carriage return "
                        + $"(offset = {lineStartOffset}, line = {currentLine.LineNumber})"
                );
            }
            else
            {
                // Also remove everything on current line
                // We'll temporarily do this for now to fix progress
                var lineEndOffset = currentLine.EndOffset;
                var lineLength = lineEndOffset - lineStartOffset;
                Document.Remove(lineStartOffset, lineLength);

                Logger.ConditionalTrace(
                    $"Moving cursor to start for carriage return " + $"({writeCursor} -> {lineStartOffset})"
                );
                writeCursor = lineStartOffset;
            }
        }

        // Write new text
        if (!string.IsNullOrEmpty(output.Text))
        {
            DirectWriteLinesToConsole(output.Text);
        }

        // Handle cursor movements
        if (output.CursorUp > 0)
        {
            // Get the line and column of the current cursor
            var currentLocation = Document.GetLocation(writeCursor);

            if (currentLocation.Line == 1)
            {
                // We are already on the first line, ignore
                Logger.ConditionalTrace($"Cursor up: Already on first line");
            }
            else
            {
                // We want to move up one line
                var targetLocation = new TextLocation(currentLocation.Line - 1, currentLocation.Column);
                var targetOffset = Document.GetOffset(targetLocation);

                // Update cursor to target offset
                Logger.ConditionalTrace(
                    $"Cursor up: Moving (line {currentLocation.Line}, {writeCursor})"
                        + $" -> (line {targetLocation.Line}, {targetOffset})"
                );

                writeCursor = targetOffset;
            }
        }

        // Handle erase commands, different to cursor move as they don't move the cursor
        // We'll insert blank spaces instead
        if (output.AnsiCommand.HasFlag(AnsiCommand.EraseLine))
        {
            // Get the current line, we'll insert spaces from start to end
            var currentLine = Document.GetLineByOffset(writeCursor);
            // Must be smaller than total lines
            currentLine =
                currentLine.LineNumber < Document.LineCount
                    ? currentLine
                    : Document.GetLineByNumber(Document.LineCount - 1);

            // Make some spaces to insert
            var spaces = new string(' ', currentLine.Length);

            // Insert the text
            Logger.ConditionalTrace(
                $"Erasing line {currentLine.LineNumber}: (length = {currentLine.Length})"
            );
            using (Document.RunUpdate())
            {
                Document.Replace(currentLine.Offset, currentLine.Length, spaces);
            }
        }

        DebugPrintDocument();
    }

    /// <summary>
    /// Write text potentially containing line breaks to the console.
    /// <remarks>This call will hold a upgradeable read lock</remarks>
    /// </summary>
    private void DirectWriteLinesToConsole(string text)
    {
        // When our cursor is not at end, newlines should be interpreted as commands to
        // move cursor forward to the next linebreak instead of inserting a newline.

        // If text contains no newlines, we can just call DirectWriteToConsole
        // Also if cursor is equal to document length
        if (!text.Contains(Environment.NewLine) || writeCursor == Document.TextLength)
        {
            DirectWriteToConsole(text);
            return;
        }

        // Otherwise we need to handle how linebreaks are treated
        // Split text into lines
        var lines = text.Split(Environment.NewLine).ToList();

        foreach (var lineText in lines.SkipLast(1))
        {
            // Insert text
            DirectWriteToConsole(lineText);

            // Set cursor to start of next line, if we're not already there
            var currentLine = Document.GetLineByOffset(writeCursor);
            // If next line is available, move cursor to start of next line
            if (currentLine.LineNumber < Document.LineCount)
            {
                var nextLine = Document.GetLineByNumber(currentLine.LineNumber + 1);
                Logger.ConditionalTrace(
                    $"Moving cursor to start of next line " + $"({writeCursor} -> {nextLine.Offset})"
                );
                writeCursor = nextLine.Offset;
            }
            else
            {
                // Otherwise move to end of current line, and direct insert a newline
                var lineEndOffset = currentLine.EndOffset;
                Logger.ConditionalTrace(
                    $"Moving cursor to end of current line " + $"({writeCursor} -> {lineEndOffset})"
                );
                writeCursor = lineEndOffset;
                DirectWriteToConsole(Environment.NewLine);
            }
        }
    }

    /// <summary>
    /// Write text to the console, does not handle newlines.
    /// This should probably only be used by <see cref="DirectWriteLinesToConsole"/>
    /// <remarks>This call will hold a upgradeable read lock</remarks>
    /// </summary>
    private void DirectWriteToConsole(string text)
    {
        CheckMaxLines();

        using (Document.RunUpdate())
        {
            // Need to replace text first if cursor lower than document length
            var replaceLength = Math.Min(Document.TextLength - writeCursor, text.Length);
            if (replaceLength > 0)
            {
                var newText = text[..replaceLength];
                Logger.ConditionalTrace(
                    $"Replacing: (cursor = {writeCursor}, length = {replaceLength}, "
                        + $"text = {Document.GetText(writeCursor, replaceLength).ToRepr()} "
                        + $"-> {newText.ToRepr()})"
                );

                Document.Replace(writeCursor, replaceLength, newText);
                writeCursor += replaceLength;
            }

            // If we replaced less than content.Length, we need to insert the rest
            var remainingLength = text.Length - replaceLength;
            if (remainingLength > 0)
            {
                var textToInsert = text[replaceLength..];
                Logger.ConditionalTrace(
                    $"Inserting: (cursor = {writeCursor}, " + $"text = {textToInsert.ToRepr()})"
                );

                Document.Insert(writeCursor, textToInsert);
                writeCursor += textToInsert.Length;
            }
        }
    }

    private void CheckMaxLines()
    {
        // Ignore limit if MaxLines is negative
        if (MaxLines < 0)
            return;

        if (Document.LineCount <= MaxLines)
            return;

        // Minimum lines to remove
        const int removeLinesBatchSize = 1;

        using (Document.RunUpdate())
        {
            var currentLines = Document.LineCount;
            var linesExceeded = currentLines - MaxLines;
            var linesToRemove = Math.Min(currentLines, Math.Max(linesExceeded, removeLinesBatchSize));

            Logger.ConditionalTrace(
                "Exceeded max lines ({Current} > {Max}), removing {Remove} lines",
                currentLines,
                MaxLines,
                linesToRemove
            );

            // Remove lines from the start
            var firstLine = Document.GetLineByNumber(1);
            var lastLine = Document.GetLineByNumber(linesToRemove);
            var removeStart = firstLine.Offset;

            // If a next line exists, use the start offset of that instead in case of weird newlines
            var removeEnd = lastLine.EndOffset;
            if (lastLine.NextLine is not null)
            {
                removeEnd = lastLine.NextLine.Offset;
            }

            var removeLength = removeEnd - removeStart;

            Logger.ConditionalTrace(
                "Removing {LinesExceeded} lines from start: ({RemoveStart} -> {RemoveEnd})",
                linesExceeded,
                removeStart,
                removeEnd
            );

            Document.Remove(removeStart, removeLength);

            // Update cursor position
            writeCursor -= removeLength;
        }
    }

    /// <summary>
    /// Debug function to print the current document to the console.
    /// Includes formatted cursor position.
    /// </summary>
    [Conditional("DEBUG")]
    private void DebugPrintDocument()
    {
        if (!Logger.IsTraceEnabled)
            return;

        var text = Document.Text;
        // Add a number for each line
        // Add an arrow line for where the cursor is, for example (cursor on offset 3):
        //
        // 1 | This is the first line
        // ~~~~~~~^ (3)
        // 2 | This is the second line
        //

        var lines = text.Split(Environment.NewLine).ToList();
        var numberPadding = lines.Count.ToString().Length;
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i] = $"{(i + 1).ToString().PadLeft(numberPadding)} | {lines[i]}";
        }
        var cursorLine = Document.GetLineByOffset(writeCursor);
        var cursorLineOffset = writeCursor - cursorLine.Offset;

        // Need to account for padding + line number + space + cursor line offset
        var linePadding = numberPadding + 3 + cursorLineOffset;
        var cursorLineArrow = new string('~', linePadding) + $"^ ({writeCursor})";

        // If more than line count, append to end
        if (cursorLine.LineNumber >= lines.Count)
        {
            lines.Add(cursorLineArrow);
        }
        else
        {
            lines.Insert(cursorLine.LineNumber, cursorLineArrow);
        }

        var textWithCursor = string.Join(Environment.NewLine, lines);

        Logger.ConditionalTrace("[Current Document]");
        Logger.ConditionalTrace(textWithCursor);
    }

    /// <summary>
    /// Posts an update to the console
    /// <remarks>Safe to call on non-UI threads</remarks>
    /// </summary>
    public void Post(ProcessOutput output)
    {
        // If update task is running, send to buffer
        if (updateTask != null)
        {
            buffer.Post(output);
            return;
        }
        // Otherwise, use manual update one
        Logger.Debug("Synchronous post update to console: {@Output}", output);
        Dispatcher.UIThread.Post(() => ConsoleUpdateOne(output));
    }

    /// <summary>
    /// Posts an update to the console.
    /// Helper for calling Post(ProcessOutput) with strings
    /// </summary>
    public void Post(string text)
    {
        Post(new ProcessOutput { Text = text });
    }

    /// <summary>
    /// Posts an update to the console.
    /// Equivalent to Post(text + Environment.NewLine)
    /// </summary>
    public void PostLine(string text)
    {
        Post(new ProcessOutput { Text = text + Environment.NewLine });
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        updateCts?.Cancel();
        updateCts?.Dispose();
        updateCts = null;

        buffer.Complete();

        if (updateTask is not null)
        {
            Logger.Debug("Shutting down console update task");

            try
            {
                updateTask.WaitWithoutException(new CancellationTokenSource(1000).Token);
                updateTask.Dispose();
                updateTask = null;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("During shutdown - Console update task cancellation timed out");
            }
            catch (InvalidOperationException e)
            {
                Logger.Warn(e, "During shutdown - Console update task dispose failed");
            }
        }

        isDisposed = true;

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
            return;

        updateCts?.Cancel();
        updateCts?.Dispose();
        updateCts = null;

        if (updateTask is not null)
        {
            Logger.Debug("Waiting for console update task shutdown...");

            await updateTask;
            updateTask.Dispose();
            updateTask = null;

            Logger.Debug("Console update task shutdown complete");
        }

        isDisposed = true;

        GC.SuppressFinalize(this);
    }
}
