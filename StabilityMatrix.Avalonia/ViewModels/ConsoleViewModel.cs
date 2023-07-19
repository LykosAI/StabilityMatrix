using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class ConsoleViewModel : ObservableObject, IDisposable
{
    // Queue for console updates
    private BufferBlock<ProcessOutput> buffer = new();
    // Task that updates the console (runs on UI thread)
    private Task? updateTask;
    // Cancellation token source for updateTask
    private CancellationTokenSource? updateCts;
    
    public bool IsUpdatesRunning => updateTask?.IsCompleted == false;
    
    [ObservableProperty] private TextDocument document = new();
    
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
        updateCts?.Cancel();
        updateCts = null;
        if (updateTask is not null)
        {
            await updateTask;
            updateTask = null;
        }
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
                ConsoleUpdateOne(output);
            }
        }
        catch (OperationCanceledException)
        {
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
                
        using var update = Document.RunUpdate();
        // Handle instruction to clear previous lines
        if (output.ClearLines > 0)
        {
            for (var i = 0; i < output.ClearLines; i++)
            {
                var lastLineIndex = Document.LineCount - 1;
                var line = Document.Lines[lastLineIndex];
                        
                Document.Remove(line.Offset, line.Length);
            }
        }
        // Add new line
        Document.Insert(Document.TextLength, output.Text);
    }
    
    /// <summary>
    /// Posts an update to the console
    /// <remarks>Safe to call on non-UI threads</remarks>
    /// </summary>
    public void Post(ProcessOutput output)
    {
        // If update task is running, send to buffer
        if (updateTask?.IsCompleted == false)
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
