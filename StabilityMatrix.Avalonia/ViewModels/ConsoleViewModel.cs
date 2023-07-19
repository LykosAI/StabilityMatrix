using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class ConsoleViewModel : ObservableObject, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    // Queue for console updates
    private BufferBlock<ProcessOutput> buffer = new();
    // Task that updates the console (runs on UI thread)
    private Task? updateTask;
    // Cancellation token source for updateTask
    private CancellationTokenSource? updateCts;
    
    public bool IsUpdatesRunning => updateTask?.IsCompleted == false;
    
    [ObservableProperty] private TextDocument document = new();
    
    // Tracks the global write cursor offset
    private int writeCursor;
    
    // Lock for accessing the write cursor
    private readonly object writeCursorLock = new();
    
    // Special instruction events
    public event EventHandler<ApcMessage>? ApcInput;

    /// <summary>
    /// Starts update task for processing Post messages.
    /// </summary>
    public void StartUpdates()
    {
        updateCts = new CancellationTokenSource();
        updateTask = Dispatcher.UIThread.InvokeAsync(ConsoleUpdateLoop, DispatcherPriority.Render);
    }
    
    /// <summary>
    /// Cancels the update task and waits for it to complete.
    /// </summary>
    public async Task StopUpdatesAsync()
    {
        Logger.Trace($"Stopping console updates, current buffer items: {buffer.Count}");
        await Task.Delay(100);
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
    /// </summary>
    public void Clear()
    {
        // Clear document
        Document.Text = string.Empty;
        // Clear buffer and create new one
        buffer.Complete();
        buffer = new BufferBlock<ProcessOutput>();
    }
    
    private async Task ConsoleUpdateLoop()
    {
        // This must be run in the UI thread
        Dispatcher.UIThread.CheckAccess();
        // Update cancellation token must be set
        if (updateCts is null)
        {
            throw new InvalidOperationException("Update cancellation token must be set");
        }
        // Get cancellation token
        var ct = updateCts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var output = await buffer.ReceiveAsync(ct);
                Logger.Trace($"Processing output: (Text = {output.Text.ToRepr()}, " +
                             $"ClearLines = {output.CarriageReturn}, CursorUp = {output.CursorUp})");
                ConsoleUpdateOne(output);
            }
        }
        catch (InvalidOperationException e)
        {
            Logger.Info($"Console update loop stopped: {e.Message}");
        }
        catch (OperationCanceledException e)
        {
            Logger.Info($"Console update loop stopped: {e.Message}");
        }
    }

    /// <summary>
    /// Handle one instance of ProcessOutput.
    /// </summary>
    /// <remarks>Not checked, but must be run in the UI thread.</remarks>
    private void ConsoleUpdateOne(ProcessOutput output)
    {
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
        // start current write at the beginning of the last line
        if (output.CarriageReturn > 0)
        {
            var lastLineIndex = Document.LineCount - 1;
            var line = Document.Lines[lastLineIndex];
            
            // Get the start of line offset
            var lineStartOffset = line.Offset;
            
            // Use this as new write cursor
            if (writeCursor != lineStartOffset)
            {
                lock (writeCursorLock)
                {
                    writeCursor = lineStartOffset;
                }
            }
        }
        
        // Insert text
        if (!string.IsNullOrEmpty(output.Text))
        {
            var currentCursor = writeCursor;
            using var _ = Document.RunUpdate();
            // Check if the cursor is lower than the document length
            // If so, we need to replace the text first
            var replaceLength = Math.Min(Document.TextLength - currentCursor, output.Text.Length);
            if (replaceLength > 0)
            {
                Document.Replace(currentCursor, replaceLength, output.Text[..replaceLength]);
                Debug.WriteLine($"Replacing: offset = {currentCursor}, length = {replaceLength}, " +
                                $"text = {output.Text[..replaceLength].ToRepr()}");

                lock (writeCursorLock)
                {
                    writeCursor += replaceLength;
                }
            }
            // If we replaced less than content.Length, we need to insert the rest
            var remainingLength = output.Text.Length - replaceLength;
            if (remainingLength > 0)
            {
                Document.Insert(writeCursor, output.Text[replaceLength..]);
                Debug.WriteLine($"Inserting: offset = {writeCursor}, " +
                                $"text = {output.Text[replaceLength..].ToRepr()}");
                
                lock (writeCursorLock)
                {
                    writeCursor += remainingLength;
                }
            }
        }
        
        // Handle cursor movements
        if (output.CursorUp > 0)
        {
            var currentCursor = writeCursor;
            // First get the line the current cursor is on
            var currentCursorLineNum = Document.GetLineByOffset(currentCursor).LineNumber;
                
            // We want to move to the line above the current cursor line
            var previousLineNum = Math.Min(0, currentCursorLineNum - output.CursorUp);
            var previousLine = Document.GetLineByNumber(previousLineNum);
                
            // Set the cursor to the *end* of the previous line
            Logger.Trace($"Moving cursor up ({currentCursor} -> {previousLine.EndOffset})");
            lock (writeCursorLock)
            {
                writeCursor = previousLine.EndOffset;
            }
        }
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
        updateCts?.Cancel();
        updateTask?.Dispose();
        updateCts?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
