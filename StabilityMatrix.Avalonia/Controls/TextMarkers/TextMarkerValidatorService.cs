using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;

namespace StabilityMatrix.Avalonia.Controls.TextMarkers;

public class TextMarkerValidatorService
{
    private string? currentText;
    private Task? currentTask;
    
    private TimeSpan updateInterval;

    public EventHandler<TextMarkerValidationEventArgs>? ValidationUpdate;
    
    private void OnValidationUpdate(TextMarkerValidationEventArgs e)
    {
        ValidationUpdate?.Invoke(this, e);
    }
    
    public TextMarkerValidatorService(TimeSpan updateInterval)
    {
        this.updateInterval = updateInterval;
    }
    
    public void UpdateText(string text)
    {
        // Ignore if text is the same
        if (currentText == text) return;
        
        // If previous task is not null, ignore
        if (currentTask != null) return;
        
        // Start a task to validate the text, and delay it by the update interval after the last update
        currentTask = Task.Run(async () =>
        {
            await ValidateWithDelayAsync();
        }).ContinueWith(_ =>
        {
            // Set task to null
            currentTask = null;
            // Set current text
            currentText = text;
        });

        currentTask.SafeFireAndForget();
    }
    
    private void Validate()
    {
        
    }
    
    private async Task ValidateWithDelayAsync(CancellationToken cancellationToken = default)
    {
        // Validate the text
        Validate();
        
        // Wait for the update interval
        await Task.Delay(updateInterval, cancellationToken);
    }
}
