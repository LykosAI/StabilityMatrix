using System;
using Avalonia.Interactivity;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

public class InsertionRequestEventArgs : EventArgs
{
    public required ICompletionData Item { get; init; }
    public required RoutedEventArgs TriggeringEvent { get; init; }
    
    public string? AppendText { get; init; }
}
